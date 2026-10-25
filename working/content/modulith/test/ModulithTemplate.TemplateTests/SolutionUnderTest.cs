using Mediator;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModulithTemplate.Features.Orders.Domain.Entities;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.Features.Orders.Web;
using ModulithTemplate.Features.Payments.Domain.Entities;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.Features.Payments.Web;
using ModulithTemplate.SharedKernel.Application.Behaviours;
using ModulithTemplate.SharedKernel.Outbox;
using ModulithTemplate.SharedKernel.Outbox.Data;

using Underground.Outbox.Configuration;
using Underground.Outbox.Data;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// The generated solution's composition, wired as <c>Program.cs</c> wires it, over a Postgres
/// database of this test's own.
/// </summary>
/// <remarks>
/// The feature modules, the outbox registration, the mediator options and the provider are all the
/// real ones, so a binding this design depends on cannot be missing in the host yet present here.
/// Only the connection string differs, which is also why nothing here rebuilds the options
/// <c>AddModuleDbContext</c> registered.
/// </remarks>
internal sealed class SolutionUnderTest : IAsyncDisposable
{
    private readonly TestDatabase _database;
    private readonly ServiceProvider _provider;

    /// <summary>What each save through the Orders context was about to write.</summary>
    public SaveRecorder OrdersSaves { get; }

    private SolutionUnderTest(TestDatabase database, ServiceProvider provider, SaveRecorder ordersSaves)
    {
        _database = database;
        _provider = provider;
        OrdersSaves = ordersSaves;
    }

    /// <summary>Builds the solution's services over a fresh database.</summary>
    /// <param name="configure">The test's own additions, applied after the solution's registrations.</param>
    /// <returns>The composed solution.</returns>
    public static SolutionUnderTest Start(Action<WebApplicationBuilder>? configure = null)
    {
        var database = TestDatabase.Create();
        ServiceProvider? provider = null;

        try
        {
            var ordersSaves = new SaveRecorder();
            (provider, var contexts) = Compose(database.ConnectionString, ordersSaves, configure);
            CreateSchema(provider, contexts);

            return new SolutionUnderTest(database, provider, ordersSaves);
        }
        catch
        {
            // Nothing owns them until the instance exists, and the test never sees one.
            provider?.Dispose();
            database.Dispose();
            throw;
        }
    }

    /// <summary>Registers the solution's services against this test's database.</summary>
    /// <param name="connectionString">The test's own database.</param>
    /// <param name="ordersSaves">Records the saves made through the Orders context.</param>
    /// <param name="configure">The test's own additions.</param>
    /// <returns>The built container, and the context types it registered.</returns>
    private static (ServiceProvider Provider, Type[] Contexts) Compose(
        string connectionString, SaveRecorder ordersSaves, Action<WebApplicationBuilder>? configure)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:PostgresConnection"] = connectionString,
        });
        builder.Services.AddLogging(logging => logging.ClearProviders());

        // From here to AddMediator: Program.cs, in its order. Each feature module contributes its own
        // outbox handlers, so what the worker can deliver here is what the host can deliver.
        builder.Services.AddOutboxDbContext(builder.Configuration);
        builder.Services.AddOutboxServices<OutboxContext>(_ => { });
        builder.Services.AddInboxDbContext(builder.Configuration);
        builder.Services.AddInboxServices<InboxContext>(_ => { });
        builder.ConfigureOrdersFeature();
        builder.ConfigurePaymentsFeature();
        builder.Services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehaviour<,>),
                typeof(ExceptionBehaviour<,>),
                typeof(ValidationBehaviour<,>),
            ];
        });

        // After the interceptors AddModuleDbContext attached, so it sees what their handlers staged.
        builder.Services.ConfigureContext<OrdersContext>(options => options.AddInterceptors(ordersSaves));
        configure?.Invoke(builder);

        Type[] contexts =
        [
            .. builder.Services
                .Select(descriptor => descriptor.ServiceType)
                .Where(type => type.IsSubclassOf(typeof(DbContext)))
                .Distinct(),
        ];

        return (builder.Services.BuildServiceProvider(), contexts);
    }

    /// <summary>Runs a command through the real mediator pipeline, in its own scope.</summary>
    /// <typeparam name="TResponse">The command's response.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>The handler's response.</returns>
    public async Task<TResponse> SendAsync<TResponse>(
        ICommand<TResponse> command, CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    /// <summary>A scope, as the host gives each request and the outbox worker each message.</summary>
    /// <returns>The new scope.</returns>
    public IServiceScope CreateScope() => _provider.CreateScope();

    /// <summary>
    /// Runs the library's own inbox processor, one attempt per fresh scope as its worker does, until
    /// every row has been completed or has failed once.
    /// </summary>
    /// <remarks>
    /// The processor is internal to the library, so it is reached by name. A row is offered only once
    /// every older transaction in the cluster has ended, which a parallel test can delay, hence the wait.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task completing once no row awaits its first attempt.</returns>
    /// <exception cref="TimeoutException">A row was never offered.</exception>
    public async Task DeliverAsync(CancellationToken cancellationToken)
    {
        var processor = typeof(InboxMessage).Assembly
            .GetType("Underground.Outbox.Domain.IProcessor`1", throwOnError: true)!
            .MakeGenericType(typeof(InboxMessage));
        var tryProcess = processor.GetMethod("TryProcessHeadMessageAsync")!;
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            await using var scope = _provider.CreateAsyncScope();
            var attempt = (Task)tryProcess.Invoke(
                scope.ServiceProvider.GetRequiredService(processor), [cancellationToken])!;
            await attempt;

            var claimed = attempt.GetType().GetProperty("Result")!.GetValue(attempt)!;
            if (!string.Equals(claimed.ToString(), "NothingOffered", StringComparison.Ordinal))
            {
                continue;
            }

            var awaiting = await scope.ServiceProvider.GetRequiredService<InboxContext>().InboxMessages
                .AnyAsync(row => row.CompletedAt == null && row.RetryCount == 0, cancellationToken);
            if (!awaiting)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("An inbox row was never offered for delivery.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    /// <summary>The rows in the shared outbox table, read through the context that owns it.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The staged messages.</returns>
    public async Task<IReadOnlyList<OutboxMessage>> OutboxRowsAsync(CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxContext>()
            .OutboxMessages.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>The rows in the shared inbox table, read through the context that owns it.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per Consumer of each event published.</returns>
    public async Task<IReadOnlyList<InboxMessage>> InboxRowsAsync(CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<InboxContext>()
            .InboxMessages.AsNoTracking().OrderBy(row => row.Id).ToListAsync(cancellationToken);
    }

    /// <summary>The entities the publishing feature has recorded.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entities.</returns>
    public async Task<IReadOnlyList<SomeEntity>> SomeEntitiesAsync(CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OrdersContext>()
            .Set<SomeEntity>().AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>The payments the consuming feature has recorded.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The payments.</returns>
    public async Task<IReadOnlyList<Payment>> PaymentsAsync(CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsContext>()
            .Set<Payment>().AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        _database.Dispose();
    }

    /// <summary>Creates each registered context's tables, and only the ones it owns.</summary>
    /// <remarks>
    /// Per context rather than <c>EnsureCreated</c>, which creates nothing once the database has any
    /// table. Tables excluded from a context's migrations are skipped, so only <c>OutboxContext</c> and
    /// <c>InboxContext</c> create the shared tables — the same division of labour the migrations have.
    /// From the model rather than by migrating, because the template ships none.
    /// </remarks>
    /// <param name="provider">The built container.</param>
    /// <param name="contexts">Every context type the composition registered.</param>
    private static void CreateSchema(ServiceProvider provider, IEnumerable<Type> contexts)
    {
        using var scope = provider.CreateScope();
        foreach (var context in contexts)
        {
            ((DbContext)scope.ServiceProvider.GetRequiredService(context))
                .GetService<IRelationalDatabaseCreator>().CreateTables();
        }
    }
}

using Mediator;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <returns>The composed solution.</returns>
    public static SolutionUnderTest Start()
    {
        var database = TestDatabase.Create();
        ServiceProvider? provider = null;

        try
        {
            var ordersSaves = new SaveRecorder();
            provider = Compose(database.ConnectionString, ordersSaves);
            CreateSchema(provider);

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
    /// <returns>The built container.</returns>
    private static ServiceProvider Compose(string connectionString, SaveRecorder ordersSaves)
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

        // Added to the registered options rather than rebuilt into them, so the recorder runs after
        // the interceptors AddModuleDbContext attached and sees what their handlers staged.
        builder.Services.AddSingleton<IDbContextOptionsConfiguration<OrdersContext>>(
            new AttachInterceptor<OrdersContext>(ordersSaves));

        return builder.Services.BuildServiceProvider();
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

    /// <summary>The rows in the shared outbox table, read through the context that owns it.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The staged messages.</returns>
    public async Task<IReadOnlyList<OutboxMessage>> OutboxRowsAsync(CancellationToken cancellationToken)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxContext>()
            .OutboxMessages.AsNoTracking().ToListAsync(cancellationToken);
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

    /// <summary>Creates each context's tables, and only the ones it owns.</summary>
    /// <remarks>
    /// Per context rather than <c>EnsureCreated</c>, which creates nothing once the database has any
    /// table. Tables excluded from a context's migrations are skipped, so only <c>OutboxContext</c>
    /// creates the shared outbox table — the same division of labour the migrations have. From the
    /// model rather than by migrating, because the template ships none: the generated project's
    /// owner adds the first.
    /// </remarks>
    /// <param name="provider">The built container.</param>
    private static void CreateSchema(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        CreateTables(scope.ServiceProvider.GetRequiredService<OutboxContext>());
        CreateTables(scope.ServiceProvider.GetRequiredService<OrdersContext>());
        CreateTables(scope.ServiceProvider.GetRequiredService<PaymentsContext>());

        static void CreateTables(DbContext context) =>
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    /// <summary>Adds an interceptor to a context the solution has already registered.</summary>
    /// <typeparam name="TContext">The context to attach to.</typeparam>
    /// <param name="interceptor">The interceptor to add, after the registered ones.</param>
    private sealed class AttachInterceptor<TContext>(IInterceptor interceptor)
        : IDbContextOptionsConfiguration<TContext>
        where TContext : DbContext
    {
        /// <inheritdoc />
        public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.AddInterceptors(interceptor);
    }
}

using Mediator;

using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox.Configuration;
using Underground.Outbox.Data;

namespace ModulithTemplate.AtomicityTests;

/// <summary>
/// The generated solution's composition, wired as <c>Program.cs</c> wires it but with every context
/// moved onto one in-memory SQLite database.
/// </summary>
/// <remarks>
/// The feature modules, the outbox registration and the mediator options are the real ones, so a
/// binding this design depends on cannot be missing in the host yet present here. Only the provider
/// is swapped, and by rebuilding the registered options rather than writing new ones — see
/// <see cref="SqliteRebinding"/>, which is what keeps the interceptors under test the ones
/// <c>AddModuleDbContext</c> attached.
/// </remarks>
internal sealed class SolutionUnderTest : IAsyncDisposable
{
    /// <summary>Syntactically valid and never connected to; SQLite serves every context.</summary>
    private const string PostgresConnection =
        "Host=localhost;Database=modulith_tests;Search Path=shared,public";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    /// <summary>What each save through the Orders context was about to write.</summary>
    public SaveRecorder OrdersSaves { get; }

    private SolutionUnderTest(SqliteConnection connection, ServiceProvider provider, SaveRecorder ordersSaves)
    {
        _connection = connection;
        _provider = provider;
        OrdersSaves = ordersSaves;
    }

    /// <summary>Builds the solution's services over a fresh in-memory database.</summary>
    /// <returns>The composed solution.</returns>
    public static SolutionUnderTest Start()
    {
        // Held open for the lifetime of the test: an in-memory SQLite database exists only as long
        // as a connection to it does.
        var connection = new SqliteConnection("Data Source=:memory:");
        ServiceProvider? provider = null;

        try
        {
            connection.Open();

            // The outbox columns the library defaults in SQL. Only the outbox worker reads them —
            // these tests reach the row before any claim — so a plausible value each is enough.
            connection.CreateFunction("clock_timestamp", () => DateTime.UtcNow);
            connection.CreateFunction("pg_current_xact_id", () => 0L);

            var ordersSaves = new SaveRecorder();
            provider = Compose(connection, ordersSaves);
            CreateSchema(provider);

            return new SolutionUnderTest(connection, provider, ordersSaves);
        }
        catch
        {
            // Nothing owns them until the instance exists, and the test never sees one.
            provider?.Dispose();
            connection.Dispose();
            throw;
        }
    }

    /// <summary>Registers the solution's services, with every context served from SQLite.</summary>
    /// <param name="connection">The open in-memory connection.</param>
    /// <param name="ordersSaves">Records the saves made through the Orders context.</param>
    /// <returns>The built container.</returns>
    private static ServiceProvider Compose(SqliteConnection connection, SaveRecorder ordersSaves)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:PostgresConnection"] = PostgresConnection,
        });
        builder.Services.AddLogging(logging => logging.ClearProviders());

        // From here to AddMediator: Program.cs, in its order, which is load-bearing — the delivery
        // registration only wins because it follows AddOutboxServices.
        builder.Services.AddOutboxDbContext(builder.Configuration);
        builder.Services.AddOutboxServices<OutboxContext>(_ => { });
        builder.Services.AddIntegrationEventDelivery();
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

        builder.Services.OnSqlite<OutboxContext>(connection);
        builder.Services.OnSqlite<OrdersContext>(connection, ordersSaves);
        builder.Services.OnSqlite<PaymentsContext>(connection);

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
        await _connection.DisposeAsync();
    }

    /// <summary>Creates each context's tables, and only the ones it owns.</summary>
    /// <remarks>
    /// Per context rather than <c>EnsureCreated</c>, which creates nothing once the database has any
    /// table. Tables excluded from a context's migrations are skipped, so only <c>OutboxContext</c>
    /// creates the shared outbox table — the same division of labour the migrations have.
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
}

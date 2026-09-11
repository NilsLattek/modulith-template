using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Outbox.Data;

using Npgsql;

namespace ModulithTemplate.SharedKernel.Outbox;

public static class Configuration
{
    /// <summary>
    /// Registers the context that owns the shared outbox table's schema and migrations.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>AddModuleDbContext</c>: that attaches domain event dispatch, and the
    /// outbox table holds no aggregates. The library names the message columns itself, so the snake
    /// case convention here only carries the index names into the style the rest of the DDL uses.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">Supplies the <c>PostgresConnection</c> connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOutboxDbContext(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("PostgresConnection");
        RequireOutboxOnSearchPath(connectionString);

        return services.AddDbContext<OutboxContext>(options => options
            .UseNpgsql(
                connectionString,
                pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", OutboxSchema.Name))
            .UseSnakeCaseNamingConvention());
    }

    /// <summary>
    /// Fails startup when the connection cannot resolve the outbox table unqualified.
    /// </summary>
    /// <remarks>
    /// Without this the app starts and migrates cleanly, then every delivery attempt fails deep in a
    /// background poll with <c>relation "outbox" does not exist</c> — the failure ADR 0001 warns
    /// about, in the place it is least likely to be noticed. A connection string set outside
    /// development is the easy way to lose the setting, so it is checked wherever it comes from.
    /// </remarks>
    /// <param name="connectionString">The configured Postgres connection string.</param>
    private static void RequireOutboxOnSearchPath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No 'PostgresConnection' connection string is configured.");
        }

        var searchPath = new NpgsqlConnectionStringBuilder(connectionString).SearchPath;
        var onPath = searchPath?
            .Split(',')
            .Any(schema => schema.Trim().Equals(OutboxSchema.Name, StringComparison.Ordinal));

        if (onPath != true)
        {
            throw new InvalidOperationException(
                $"The 'PostgresConnection' connection string must put '{OutboxSchema.Name}' on its "
                + $"Search Path (e.g. 'Search Path={OutboxSchema.Name},public'): the outbox library "
                + "resolves its table unqualified.");
        }
    }
}

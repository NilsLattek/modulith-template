using Npgsql;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// A Postgres database of its own for one test, dropped when the test ends.
/// </summary>
/// <remarks>
/// A database rather than a schema: the schemas are fixed names the contexts map to, so two tests
/// sharing a database would share <c>orders</c>, <c>payments</c> and <c>shared</c> and see each
/// other's rows.
/// </remarks>
internal sealed class TestDatabase : IDisposable
{
    /// <summary>The devcontainer's database, and the CI service container's.</summary>
    private const string DefaultServer = "Host=localhost;Username=postgres;Password=postgres";

    /// <summary>Overrides <see cref="DefaultServer"/> where the server lives elsewhere.</summary>
    private const string ServerVariable = "MODULITH_TEST_POSTGRES";

    private readonly string _name;

    /// <summary>What the solution is configured with — this database, on the outbox's search path.</summary>
    public string ConnectionString { get; }

    private TestDatabase(string name, string connectionString)
    {
        _name = name;
        ConnectionString = connectionString;
    }

    /// <summary>Creates an empty database for the calling test.</summary>
    /// <returns>The created database.</returns>
    /// <exception cref="InvalidOperationException">No server answered.</exception>
    public static TestDatabase Create()
    {
        var name = $"modulith_tests_{Guid.NewGuid():N}";
        var server = Environment.GetEnvironmentVariable(ServerVariable) ?? DefaultServer;

        Execute(Server(server), $"""CREATE DATABASE "{name}" """);

        // Search Path is not a nicety: AddOutboxDbContext refuses a connection string without the
        // outbox's schema on it, for the reason ADR 0001 gives.
        var connectionString = new NpgsqlConnectionStringBuilder(server)
        {
            Database = name,
            SearchPath = "shared,public",
        }.ConnectionString;

        return new TestDatabase(name, connectionString);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The pool holds connections to a database Postgres will not drop while they are open.
        NpgsqlConnection.ClearAllPools();
        Execute(Server(ConnectionString), $"""DROP DATABASE IF EXISTS "{_name}" WITH (FORCE)""");
    }

    /// <summary>The same server, on the database every cluster has.</summary>
    /// <param name="connectionString">The connection string to redirect.</param>
    /// <returns>A connection string to the maintenance database.</returns>
    private static string Server(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", SearchPath = null }
            .ConnectionString;

    /// <summary>Runs one statement, translating an unreachable server into a usable message.</summary>
    /// <param name="connectionString">Where to run it.</param>
    /// <param name="sql">The statement.</param>
    private static void Execute(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);

        try
        {
            connection.Open();
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                $"These tests need a Postgres server. The devcontainer runs one at {DefaultServer}; "
                + $"set {ServerVariable} to a connection string if yours is elsewhere.",
                exception);
        }

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

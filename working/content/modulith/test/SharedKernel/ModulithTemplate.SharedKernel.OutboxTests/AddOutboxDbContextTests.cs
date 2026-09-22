using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Outbox;
using ModulithTemplate.SharedKernel.Outbox.Data;

namespace ModulithTemplate.SharedKernel.OutboxTests;

/// <summary>
/// Covers the startup guard on the connection string's search path.
/// </summary>
/// <remarks>
/// The library resolves its table unqualified, so a connection without the outbox schema on its
/// search path migrates and starts cleanly and then fails on every delivery attempt, inside a
/// background poll. Registration is the last place that failure can still be made loud.
/// </remarks>
public class AddOutboxDbContextTests
{
    private static IServiceCollection Register(string? connectionString)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:PostgresConnection"] = connectionString,
        };

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddOutboxDbContext(configuration);
    }

    [Theory]
    [InlineData("Host=h;Database=d;Search Path=shared")]
    [InlineData("Host=h;Database=d;Search Path=shared,public")]
    [InlineData("Host=h;Database=d;Search Path=public, shared")]
    public void Registers_the_context_when_the_outbox_schema_is_on_the_search_path(string connectionString)
    {
        var provider = Register(connectionString).BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<OutboxContext>());
    }

    [Theory]
    [InlineData("Host=h;Database=d")]
    [InlineData("Host=h;Database=d;Search Path=public")]
    // "shared_events" contains "shared" but is a different schema, so a substring test would pass it.
    [InlineData("Host=h;Database=d;Search Path=shared_events")]
    public void Throws_when_the_outbox_schema_is_not_on_the_search_path(string connectionString)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Register(connectionString));

        Assert.Contains("Search Path", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Throws_when_no_connection_string_is_configured()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Register(connectionString: null));

        Assert.Contains("PostgresConnection", error.Message, StringComparison.Ordinal);
    }
}

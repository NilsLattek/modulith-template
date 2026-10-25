using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Outbox;
using ModulithTemplate.SharedKernel.Outbox.Data;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.OutboxTests;

/// <summary>
/// Pins where the inbox table lands and what its registration demands, for the reasons
/// <see cref="OutboxContextModelTests"/> and <see cref="AddOutboxDbContextTests"/> give for the outbox.
/// </summary>
public class InboxContextModelTests
{
    private static IEntityType MessageEntity()
    {
        var options = new DbContextOptionsBuilder<InboxContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var context = new InboxContext(options);
        return context.Model.FindEntityType(typeof(InboxMessage))!;
    }

    [Fact]
    public void InboxMessage_is_mapped_into_the_schema_the_search_path_names()
    {
        Assert.Equal(OutboxSchema.Name, MessageEntity().GetSchema());
    }

    [Fact]
    public void InboxMessage_is_mapped_to_the_table_the_claim_sql_names()
    {
        Assert.Equal("inbox", MessageEntity().GetTableName());
    }

    [Fact]
    public void Registration_throws_when_the_shared_schema_is_not_on_the_search_path()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:PostgresConnection"] = "Host=h;Database=d;Search Path=public",
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInboxDbContext(configuration));

        Assert.Contains("Search Path", error.Message, StringComparison.Ordinal);
    }
}

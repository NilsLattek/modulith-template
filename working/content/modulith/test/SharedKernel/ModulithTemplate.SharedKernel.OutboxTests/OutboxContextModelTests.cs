using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using ModulithTemplate.SharedKernel.Outbox.Data;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.OutboxTests;

/// <summary>
/// Pins where the outbox table lands, which the library's claim SQL cannot say for itself.
/// </summary>
/// <remarks>
/// That SQL reads <c>FROM outbox</c> — unqualified, resolved through the connection's
/// <c>Search Path</c>. A context that migrates the table into a different schema still compiles,
/// migrates and starts, then fails at the first claim, so the pairing is worth pinning. Model
/// assertions only; no database server is needed.
/// </remarks>
public class OutboxContextModelTests
{
    private static IEntityType MessageEntity()
    {
        var options = new DbContextOptionsBuilder<OutboxContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var context = new OutboxContext(options);
        return context.Model.FindEntityType(typeof(OutboxMessage))!;
    }

    [Fact]
    public void OutboxMessage_is_mapped_into_the_schema_the_search_path_names()
    {
        Assert.Equal(OutboxSchema.Name, MessageEntity().GetSchema());
    }

    [Fact]
    public void OutboxMessage_is_mapped_to_the_table_the_claim_sql_names()
    {
        Assert.Equal("outbox", MessageEntity().GetTableName());
    }
}

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>The database schema holding the shared outbox and inbox tables.</summary>
/// <remarks>
/// Named rather than repeated as a literal because three places must agree on it: the owning contexts'
/// default schema, their migrations history tables, and the <c>Search Path</c> on the connection string
/// that lets the library's unqualified claim SQL resolve <c>outbox</c> and <c>inbox</c>.
/// </remarks>
public static class OutboxSchema
{
    /// <summary>The schema name.</summary>
    public const string Name = "shared";

    /// <summary>The outbox table's name, which the library fixes and this solution may not remap.</summary>
    public const string TableName = "outbox";

    /// <summary>The inbox table's name, fixed by the library likewise.</summary>
    public const string InboxTableName = "inbox";
}

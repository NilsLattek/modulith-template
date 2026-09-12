namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>The database schema holding the shared outbox table.</summary>
/// <remarks>
/// Named rather than repeated as a literal because three places must agree on it: this context's
/// default schema, its migrations history table, and the <c>Search Path</c> on the connection string
/// that lets the library's unqualified claim SQL resolve <c>outbox</c>.
/// </remarks>
public static class OutboxSchema
{
    /// <summary>The schema name.</summary>
    public const string Name = "shared";

    /// <summary>The table name, which the library fixes and this solution may not remap.</summary>
    public const string TableName = "outbox";
}

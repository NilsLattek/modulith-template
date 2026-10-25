using Microsoft.EntityFrameworkCore;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>Model configuration a feature applies to stage into the shared outbox and inbox tables.</summary>
public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Maps this context's outbox set onto the one shared table, and keeps it out of this feature's
    /// migrations.
    /// </summary>
    /// <remarks>
    /// Every feature context reaches this through <c>ApplySharedModel()</c>; <see cref="OutboxContext"/>
    /// does not, because it owns the DDL. Omitting it compiles and starts, then maps the
    /// entity into the feature's own schema and tries to create a second outbox table — which is what
    /// <c>SharedTableMappingTests</c> in the architecture tests exists to catch.
    /// </remarks>
    /// <param name="modelBuilder">The model being built, from <c>OnModelCreating</c>.</param>
    /// <returns>The same model builder, for chaining.</returns>
    public static ModelBuilder MapSharedOutbox(this ModelBuilder modelBuilder)
    {
        // An explicit per-entity table and schema overrides the context's default schema, wherever in
        // OnModelCreating this is called.
        modelBuilder.Entity<OutboxMessage>()
            .ToTable(OutboxSchema.TableName, OutboxSchema.Name, table => table.ExcludeFromMigrations());

        return modelBuilder;
    }

    /// <summary>
    /// Maps this context's inbox set onto the one shared table, and keeps it out of this feature's
    /// migrations.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="MapSharedOutbox"/>: <see cref="InboxContext"/> owns the DDL, and
    /// <c>SharedTableMappingTests</c> in the architecture tests holds every feature context to this mapping.
    /// </remarks>
    /// <param name="modelBuilder">The model being built, from <c>OnModelCreating</c>.</param>
    /// <returns>The same model builder, for chaining.</returns>
    public static ModelBuilder MapSharedInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>()
            .ToTable(OutboxSchema.InboxTableName, OutboxSchema.Name, table => table.ExcludeFromMigrations());

        return modelBuilder;
    }
}

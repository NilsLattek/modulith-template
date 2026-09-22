using Microsoft.EntityFrameworkCore;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>Model configuration a feature applies to stage into the one shared outbox table.</summary>
public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Maps this context's outbox set onto the one shared table, and keeps it out of this feature's
    /// migrations.
    /// </summary>
    /// <remarks>
    /// Every feature context implementing <c>IOutboxDbContext</c> calls this; <see cref="OutboxContext"/>
    /// does not, because it owns the DDL. Omitting it compiles and starts, then maps the
    /// entity into the feature's own schema and tries to create a second outbox table — which is what
    /// <c>OutboxMappingTests</c> in the architecture tests exists to catch.
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
}

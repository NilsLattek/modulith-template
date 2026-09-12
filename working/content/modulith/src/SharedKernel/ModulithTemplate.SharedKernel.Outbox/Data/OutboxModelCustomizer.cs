using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>
/// Maps a feature context's outbox set onto the one shared table, and keeps it out of that
/// feature's migrations.
/// </summary>
/// <remarks>
/// Applied by the shared context registration rather than by each feature's
/// <c>OnModelCreating</c>: a feature that forgot it would map the entity into its own schema and
/// try to create a second outbox table of its own. <see cref="OutboxContext"/> owns the DDL
/// (ADR 0001).
/// </remarks>
/// <param name="dependencies">EF Core's customizer dependencies.</param>
public sealed class OutboxModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    /// <inheritdoc />
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.Customize(modelBuilder, context);

        if (context is not IOutboxDbContext)
        {
            return;
        }

        // After base.Customize, which runs the context's own OnModelCreating: a per-entity table and
        // schema mapping overrides the default schema set there.
        modelBuilder.Entity<OutboxMessage>()
            .ToTable(OutboxSchema.TableName, OutboxSchema.Name, table => table.ExcludeFromMigrations());
    }
}

using Microsoft.EntityFrameworkCore;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>
/// Owns the DDL for the one shared outbox table every feature stages its integration events into.
/// </summary>
/// <remarks>
/// The only context that migrates <c>shared.outbox</c>; feature contexts map the same entity with
/// <c>ExcludeFromMigrations()</c>. See ADR 0001 for why the table is shared rather than per feature.
/// </remarks>
/// <param name="options">The context options, supplied by <c>AddOutboxDbContext</c>.</param>
public class OutboxContext(DbContextOptions<OutboxContext> options) : DbContext(options), IOutboxDbContext
{
    /// <summary>The staged integration events awaiting delivery.</summary>
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The table itself is configured by the library, through an EntityTypeConfigurationAttribute
        // on OutboxMessage; this only decides which schema it lands in.
        modelBuilder.HasDefaultSchema(OutboxSchema.Name);
    }
}

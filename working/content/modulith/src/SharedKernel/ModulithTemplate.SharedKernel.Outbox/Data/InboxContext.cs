using Microsoft.EntityFrameworkCore;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>
/// Owns the DDL for the one shared inbox table, holding one row per Consumer of each Integration Event.
/// </summary>
/// <remarks>
/// The only context that migrates <c>shared.inbox</c>; feature contexts map the same entity with
/// <c>ExcludeFromMigrations()</c>, so a Publisher stages its rows in the save that persists its aggregate.
/// The library's inbox worker claims through this context and runs each Consumer in its transaction.
/// </remarks>
/// <param name="options">The context options, supplied by <c>AddInboxDbContext</c>.</param>
public class InboxContext(DbContextOptions<InboxContext> options) : DbContext(options), IInboxDbContext
{
    /// <summary>The Integration Events awaiting their Consumers.</summary>
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // As on OutboxContext: the library configures the table, this only picks its schema.
        modelBuilder.HasDefaultSchema(OutboxSchema.Name);
    }
}

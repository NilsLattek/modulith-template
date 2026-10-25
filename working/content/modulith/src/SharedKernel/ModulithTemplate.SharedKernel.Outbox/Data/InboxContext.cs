using Microsoft.EntityFrameworkCore;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>
/// Owns the DDL for the one shared inbox table, holding one row per Consumer of each Integration Event.
/// </summary>
/// <remarks>
/// The only context that maps <c>shared.inbox</c>: the publishing feature's outbox handler saves the
/// Consumers' rows through it, and the library's inbox worker claims through it and runs each Consumer
/// in its transaction.
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

using System.Reflection;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Infrastructure;

using Underground.Outbox.Data;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data;

public class PaymentsContext(DbContextOptions<PaymentsContext> options)
    : DbContext(options), IOutboxDbContext, IInboxDbContext
{
    /// <summary>The integration events this feature has staged, in the one shared outbox table.</summary>
    /// <remarks>
    /// Implementing <see cref="IOutboxDbContext"/> is what lets this feature stage an event in the
    /// save that persists its aggregate. <see cref="SharedModelBuilderExtensions.ApplySharedModel"/>
    /// below is what maps it onto <c>shared.outbox</c> and keeps it out of this feature's migrations.
    /// </remarks>
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    /// <summary>The Consumers' rows of the integration events this feature has staged, in the shared inbox table.</summary>
    /// <remarks>
    /// Implementing <see cref="IInboxDbContext"/> is what lets this feature's save stage one row per
    /// Consumer, and lets its inbox handlers enlist this context onto the inbox's transaction.
    /// </remarks>
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.ApplySharedModel();
    }
}

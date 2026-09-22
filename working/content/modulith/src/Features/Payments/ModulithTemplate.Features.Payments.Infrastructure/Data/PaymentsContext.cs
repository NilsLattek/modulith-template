using System.Reflection;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Outbox.Data;

using Underground.Outbox.Data;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data;

public class PaymentsContext(DbContextOptions<PaymentsContext> options) : DbContext(options), IOutboxDbContext
{
    /// <summary>The integration events this feature has staged, in the one shared outbox table.</summary>
    /// <remarks>
    /// Implementing <see cref="IOutboxDbContext"/> is what lets this feature stage an event in the
    /// save that persists its aggregate. <see cref="OutboxModelBuilderExtensions.MapSharedOutbox"/>
    /// below is what maps it onto <c>shared.outbox</c> and keeps it out of this feature's migrations.
    /// </remarks>
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.MapSharedOutbox();
    }
}

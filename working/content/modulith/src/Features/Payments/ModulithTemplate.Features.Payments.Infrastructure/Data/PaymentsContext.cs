using System.Reflection;

using Microsoft.EntityFrameworkCore;

using Underground.Outbox.Data;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data;

public class PaymentsContext(DbContextOptions<PaymentsContext> options) : DbContext(options), IOutboxDbContext
{
    /// <summary>The integration events this feature has staged, in the one shared outbox table.</summary>
    /// <remarks>
    /// Implementing <see cref="IOutboxDbContext"/> is what lets this feature stage an event in the
    /// save that persists its aggregate. The mapping onto <c>shared.outbox</c>, and its exclusion
    /// from this feature's migrations, are applied by <c>AddModuleDbContext</c>.
    /// </remarks>
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

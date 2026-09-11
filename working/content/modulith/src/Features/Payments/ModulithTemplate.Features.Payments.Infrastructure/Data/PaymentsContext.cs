using System.Reflection;

using Microsoft.EntityFrameworkCore;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data;

public class PaymentsContext(DbContextOptions<PaymentsContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

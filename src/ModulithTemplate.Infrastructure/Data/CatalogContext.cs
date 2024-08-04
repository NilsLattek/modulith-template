using System.Reflection;

using Microsoft.EntityFrameworkCore;

namespace ModulithTemplate.Infrastructure;

public class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options) : base(options) { }


#pragma warning disable S125 // Sections of code should not be commented out
    // public DbSet<Basket> Baskets { get; set; }
#pragma warning restore S125 // Sections of code should not be commented out

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

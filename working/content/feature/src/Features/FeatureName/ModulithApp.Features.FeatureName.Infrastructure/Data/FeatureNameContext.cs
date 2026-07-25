using System.Reflection;

using Microsoft.EntityFrameworkCore;

namespace ModulithApp.Features.FeatureName.Infrastructure.Data;

public class FeatureNameContext(DbContextOptions<FeatureNameContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("featureschema");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

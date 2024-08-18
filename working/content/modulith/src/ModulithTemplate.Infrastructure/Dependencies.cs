using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.FeatureCore;
using ModulithTemplate.Infrastructure.Data;

namespace ModulithTemplate.Infrastructure;

public static class Dependencies
{
    public static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        services.AddDbContext<CatalogContext>(options =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("PostgresConnection"),
                    pgConfig => pgConfig.MigrationsHistoryTable(tableName: "__EFMigrationsHistory", schema: "catalog")
                )
                .UseSnakeCaseNamingConvention()
        );
    }
}

using EntityFramework.Exceptions.PostgreSQL;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.Infrastructure.Common;

public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : DbContext =>
        services.AddDbContext<TContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("PostgresConnection"),
                pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseSnakeCaseNamingConvention()
            .UseExceptionProcessor());
}

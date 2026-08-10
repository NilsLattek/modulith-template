using EntityFramework.Exceptions.PostgreSQL;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ModulithTemplate.Infrastructure.Common.Events;

namespace ModulithTemplate.Infrastructure.Common;

public static class ModuleDbContextExtensions
{
    /// <summary>
    /// Registers a feature's <see cref="DbContext"/> with this solution's shared EF Core conventions
    /// and its domain event dispatch.
    /// </summary>
    /// <remarks>
    /// The dispatcher and interceptor are registered with <c>TryAdd</c> because every feature calls
    /// this method: the registration is solution-wide, while the interceptor instance is resolved
    /// per scope and attached to each feature's own context.
    /// </remarks>
    /// <typeparam name="TContext">The feature's context type.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">Supplies the <c>PostgresConnection</c> connection string.</param>
    /// <param name="schema">The feature's database schema.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : DbContext
    {
        services.TryAddScoped<DomainEventDispatcher>();
        services.TryAddScoped<DomainEventDispatchInterceptor>();

        // The (sp, options) overload, so the interceptor comes from the same scope as the context
        // and shares the scope's mediator.
        return services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("PostgresConnection"),
                pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseSnakeCaseNamingConvention()
            .UseExceptionProcessor()
            .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));
    }
}

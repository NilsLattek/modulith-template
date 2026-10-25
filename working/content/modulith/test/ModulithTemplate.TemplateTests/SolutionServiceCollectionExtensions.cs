using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.TemplateTests;

/// <summary>Test-side additions to a composed solution.</summary>
internal static class SolutionServiceCollectionExtensions
{
    /// <summary>Adds to the options of a context the solution has already registered.</summary>
    /// <typeparam name="TContext">The context to configure.</typeparam>
    /// <param name="services">The solution's services.</param>
    /// <param name="configure">Applied after the registered configuration.</param>
    /// <returns>The same services, for chaining.</returns>
    public static IServiceCollection ConfigureContext<TContext>(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext =>
        services.AddSingleton<IDbContextOptionsConfiguration<TContext>>(new ConfigureOptions<TContext>(configure));

    private sealed class ConfigureOptions<TContext>(Action<DbContextOptionsBuilder> configure)
        : IDbContextOptionsConfiguration<TContext>
        where TContext : DbContext
    {
        /// <inheritdoc />
        public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder) =>
            configure(optionsBuilder);
    }
}

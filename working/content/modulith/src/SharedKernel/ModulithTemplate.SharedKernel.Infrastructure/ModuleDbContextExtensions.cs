using EntityFramework.Exceptions.PostgreSQL;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using ModulithTemplate.SharedKernel.Infrastructure.Events;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.SharedKernel.Infrastructure;

public static class ModuleDbContextExtensions
{
    /// <summary>
    /// Registers a feature's <see cref="DbContext"/> with this solution's shared EF Core conventions,
    /// its domain event dispatch, its half of the shared outbox, and its route into the shared inbox.
    /// </summary>
    /// <remarks>
    /// <c>TryAdd</c> because every feature calls this. The dispatcher is solution-wide; the
    /// interceptor is per context type, for the reason given on
    /// <see cref="DomainEventDispatchInterceptor{TContext}"/>.
    /// <para>
    /// The outbox's save-time interceptor is attached here; the table mappings are not, and each
    /// feature context calls <c>ApplySharedModel()</c> in its own <c>OnModelCreating</c>.
    /// </para>
    /// <para>
    /// The inbox route lets any feature's publisher address this context's envelope without naming
    /// it; a row is staged only once this feature declares a handler for the event.
    /// </para>
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
        services.TryAddScoped<DomainEventDispatchInterceptor<TContext>>();
        services.AddSingleton<IInboxRoute, InboxRoute<TContext>>();
        services.TryAddSingleton<IntegrationEventSubscriptions>();

        // The (sp, options) overload, so the interceptors come from the same scope as the context
        // and share the scope's mediator.
        return services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("PostgresConnection"),
                pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseSnakeCaseNamingConvention()
            .UseExceptionProcessor()
            .AddInterceptors(
                sp.GetRequiredService<DomainEventDispatchInterceptor<TContext>>(),
                // Constructed per context rather than resolved: it records what one transaction
                // staged in plain instance fields, so a scope saving through two feature contexts
                // would let the second discard the first's pending push. The library documents it as
                // living alongside a single context, and this is the same hazard the dispatch
                // interceptor above is generic to avoid.
                new ProcessMessagesOnSaveChangesInterceptor(
                    sp,
                    sp.GetRequiredService<ILogger<ProcessMessagesOnSaveChangesInterceptor>>()
                )
            )
        );
    }
}

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Registers the host-side plumbing that carries integration events between features.
/// </summary>
public static class IntegrationEventExtensions
{
    /// <summary>
    /// Registers the scoped <see cref="IIntegrationEventQueue"/> that feature handlers enqueue onto.
    /// </summary>
    /// <remarks>
    /// Scoped is load-bearing: the queue buffers one message's events, and the host's integration
    /// event behaviour flushes that same instance once the handler succeeds.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddIntegrationEvents(this IServiceCollection services) =>
        services.AddScoped<IIntegrationEventQueue, IntegrationEventQueue>();
}

using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.SharedKernel.Application.Events;

public static class IntegrationEventRegistration
{
    /// <summary>
    /// Declares that <typeparamref name="TEvent"/> may be delivered, so the worker can turn a stored
    /// row back into it.
    /// </summary>
    /// <remarks>
    /// Called from the owning feature's <c>Configure&lt;Name&gt;Application</c>. An event nobody
    /// registers fails loudly at delivery rather than being dropped — but note the build already
    /// rejects one with no consumer, as <c>MSG0005</c>.
    /// </remarks>
    /// <typeparam name="TEvent">The integration event to register.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddIntegrationEvent<TEvent>(this IServiceCollection services)
        where TEvent : class, IIntegrationEvent
    {
        Registry(services).Add<TEvent>();
        return services;
    }

    /// <summary>
    /// Ensures the registry exists even when no feature has registered an event.
    /// </summary>
    /// <remarks>
    /// Called by the delivery registration, so deleting the sample event — which the scaffold invites
    /// — leaves the worker reporting an unregistered type rather than failing to resolve a service.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddIntegrationEventRegistry(this IServiceCollection services)
    {
        Registry(services);
        return services;
    }

    /// <summary>
    /// The one registry instance, created on first use.
    /// </summary>
    /// <remarks>
    /// Registered as an instance rather than by type, so features can contribute in any order and
    /// the host reads a complete registry however late it resolves one.
    /// </remarks>
    /// <param name="services">The service collection holding it.</param>
    /// <returns>The registry.</returns>
    private static IntegrationEventRegistry Registry(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var registered = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IntegrationEventRegistry))
            ?.ImplementationInstance;

        if (registered is IntegrationEventRegistry existing)
        {
            return existing;
        }

        var registry = new IntegrationEventRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}

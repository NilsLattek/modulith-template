using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

public static class IntegrationEventDelivery
{
    /// <summary>
    /// Routes claimed outbox rows to the in-process republisher.
    /// </summary>
    /// <remarks>
    /// Must be called <b>after</b> the generated <c>AddOutboxServices</c>, which registers a
    /// dispatcher of its own: the later registration is the one resolved. See
    /// <see cref="IntegrationEventRepublisher"/> for why that one cannot serve.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddIntegrationEventDelivery(this IServiceCollection services)
    {
        services.AddIntegrationEventRegistry();
        return services.AddScoped<IMessageDispatcher<OutboxMessage>, IntegrationEventRepublisher>();
    }
}

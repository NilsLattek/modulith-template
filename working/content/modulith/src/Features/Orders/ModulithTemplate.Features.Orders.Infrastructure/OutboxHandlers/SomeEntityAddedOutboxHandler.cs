using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.SharedKernel.Infrastructure.Events;

namespace ModulithTemplate.Features.Orders.Infrastructure.OutboxHandlers;

/// <summary>
/// Delivers <see cref="SomeEntityAddedIntegrationEvent"/> from the outbox to its Consumers' inbox rows.
/// </summary>
/// <param name="services">The provider of the scope the outbox processes the message in.</param>
public sealed class SomeEntityAddedOutboxHandler(IServiceProvider services)
    : IntegrationEventOutboxHandler<SomeEntityAddedIntegrationEvent>(services);

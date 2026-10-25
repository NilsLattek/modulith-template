using Mediator;

using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.SharedKernel.Infrastructure.Events;

namespace ModulithTemplate.Features.Orders.Infrastructure.OutboxHandlers;

/// <summary>
/// Delivers <see cref="SomeEntityAddedIntegrationEvent"/> from the outbox to its Consumers' inbox rows.
/// </summary>
/// <param name="publisher">Publishes the event to this application's Consumers.</param>
public sealed class SomeEntityAddedOutboxHandler(IPublisher publisher)
    : IntegrationEventOutboxHandler<SomeEntityAddedIntegrationEvent>(publisher);

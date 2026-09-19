using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Contracts.Events;

using Underground.Outbox;
using Underground.Outbox.Attributes;
using Underground.Outbox.Data;

namespace ModulithTemplate.Features.Orders.Infrastructure.OutboxHandlers;

/// <summary>
/// Takes a claimed outbox row carrying this feature's event and publishes it in-process, where every
/// consumer receives it through an ordinary mediator notification handler.
/// </summary>
/// <param name="publisher">Publishes the event to this application's consumers.</param>
[MessageHandlerLifetime(ServiceLifetime.Scoped)]
public sealed class SomeEntityAddedOutboxHandler(IPublisher publisher)
    : IOutboxMessageHandler<SomeEntityAddedIntegrationEvent>
{
    public Task HandleAsync(
        SomeEntityAddedIntegrationEvent message,
        MessageMetadata metadata,
        CancellationToken cancellationToken) =>
        publisher.Publish(message, cancellationToken).AsTask();
}

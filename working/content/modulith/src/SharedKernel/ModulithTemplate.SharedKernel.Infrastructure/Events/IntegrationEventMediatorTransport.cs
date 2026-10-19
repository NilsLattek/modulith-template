using Mediator;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Takes a claimed outbox row carrying an integration event and publishes it in-process, where every
/// consumer receives it through an ordinary mediator notification handler.
/// </summary>
/// <remarks>
/// A feature derives one body-less handler per event it publishes; the outbox generator registers the
/// derived class, never this one.
/// </remarks>
/// <typeparam name="TEvent">The integration event this handler delivers.</typeparam>
/// <param name="publisher">Publishes the event to this application's consumers.</param>
public abstract class IntegrationEventMediatorTransport<TEvent>(IPublisher publisher)
    : IOutboxMessageHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    /// <inheritdoc />
    public Task HandleAsync(TEvent message, MessageMetadata metadata, CancellationToken cancellationToken) =>
        publisher.Publish(message, cancellationToken).AsTask();
}

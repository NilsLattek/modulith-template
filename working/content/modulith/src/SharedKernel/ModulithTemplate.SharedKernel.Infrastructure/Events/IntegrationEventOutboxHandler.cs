using Mediator;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Takes a claimed outbox row carrying an Integration Event and publishes it in-process: each
/// Consumer's <see cref="IntegrationEventInboxHandler{TEvent, TContext}"/> adds its inbox row, and any
/// forwarder sends it outside the process.
/// </summary>
/// <remarks>
/// A feature derives one body-less handler per event it publishes, under <c>Infrastructure/OutboxHandlers/</c>.
/// A throw retries the whole row; Consumers that already added theirs skip it on the redelivery.
/// </remarks>
/// <typeparam name="TEvent">The integration event this handler delivers.</typeparam>
/// <param name="publisher">Publishes the event to this application's Consumers and forwarders.</param>
public abstract class IntegrationEventOutboxHandler<TEvent>(IPublisher publisher)
    : IOutboxMessageHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    /// <inheritdoc />
    public Task HandleAsync(TEvent message, MessageMetadata metadata, CancellationToken cancellationToken) =>
        publisher.Publish(message, cancellationToken).AsTask();
}

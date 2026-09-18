using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Contracts.Events;

using Underground.Outbox;
using Underground.Outbox.Attributes;
using Underground.Outbox.Data;

namespace ModulithTemplate.Features.Orders.Application.OutboxHandlers;

/// <summary>
/// Takes a claimed outbox row carrying this feature's event and publishes it in-process, where every
/// consumer receives it through an ordinary mediator notification handler.
/// </summary>
/// <remarks>
/// One handler per Integration Event, in the feature whose <c>Contracts</c> declares it — the outbox
/// allows exactly one, reported as <c>OUTBOX001</c> within an assembly and thrown as
/// <c>CompetingHandlersException</c> at startup across them. Fan-out stays the mediator's: this
/// publishes once and every <c>INotificationHandler</c> runs, so a second consumer costs the
/// consuming feature a handler and this feature nothing.
/// <para>
/// It must name a <c>Contracts</c> type, which only <c>Application</c> may do, so it lives here
/// rather than beside <c>OrdersIntegrationEventPublisher</c> in <c>Web</c>.
/// </para>
/// </remarks>
/// <param name="publisher">Publishes the event to this application's consumers.</param>
[MessageHandlerLifetime(ServiceLifetime.Scoped)]
public sealed class SomeEntityAddedOutboxHandler(IPublisher publisher)
    : IOutboxMessageHandler<SomeEntityAddedIntegrationEvent>
{
    /// <inheritdoc />
    /// <remarks>
    /// The generic overload, unlike the domain event dispatcher's: both <c>Publish</c> overloads
    /// switch on the type they are given, and here that is the concrete event rather than
    /// <c>IIntegrationEvent</c>, so the mediator's generator has a branch for it. An event with no
    /// consumer is still the build failure <c>MSG0005</c> rather than a silent drop.
    /// <para>
    /// The worker gives each message its own scope, so <paramref name="message"/>'s consumers get
    /// their own <c>DbContext</c> and commit in their own transaction — never the publisher's.
    /// </para>
    /// </remarks>
    public Task HandleAsync(
        SomeEntityAddedIntegrationEvent message,
        MessageMetadata metadata,
        CancellationToken cancellationToken) =>
        publisher.Publish(message, cancellationToken).AsTask();
}

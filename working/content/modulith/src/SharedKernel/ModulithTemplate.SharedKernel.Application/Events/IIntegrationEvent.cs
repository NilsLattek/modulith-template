using Mediator;

namespace ModulithTemplate.SharedKernel.Application.Events;

/// <summary>
/// A feature's announcement to its siblings that something happened, in its published language.
/// </summary>
/// <remarks>
/// Declared as a <c>record</c> in the owning feature's <c>Contracts/Events/</c> — the only part of a
/// feature a sibling may reference. It is staged into the shared outbox by the same save that
/// persists the aggregate, so it is recorded if and only if the change it describes commits.
/// <para>
/// A feature consumes it through an <c>IntegrationEventInboxHandler</c>, which applies it <b>exactly
/// once</b> in a transaction of its own. A plain notification handler is for forwarding it outside the
/// process, and runs <b>at least once</b>.
/// </para>
/// </remarks>
public interface IIntegrationEvent : INotification
{
    /// <summary>This event's identity, from which each Consumer's row identity is derived.</summary>
    /// <remarks>
    /// Carried on the event rather than as transport metadata: delivery hands a Consumer the event
    /// alone, so anything not declared here is gone before any handler sees it.
    /// </remarks>
    Guid EventId { get; }

    /// <summary>The Group whose events are delivered one at a time, in the order they were recorded.</summary>
    /// <remarks>
    /// The aggregate the event concerns, so unrelated aggregates proceed concurrently. Declared on
    /// the event so no publish site can forget it; one shared constant would serialise the whole
    /// application behind a single stuck message.
    /// </remarks>
    string GroupKey { get; }
}

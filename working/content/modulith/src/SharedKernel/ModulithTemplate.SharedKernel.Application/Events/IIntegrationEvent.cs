namespace ModulithTemplate.SharedKernel.Application.Events;

/// <summary>
/// A feature's announcement to its siblings that something happened, in its published language.
/// </summary>
/// <remarks>
/// Declared as a <c>record</c> in the owning feature's <c>Contracts/Events/</c> — the only part of a
/// feature a sibling may reference. The save that persists the aggregate stages one shared inbox row
/// per Consumer, so it is recorded if and only if the change it describes commits.
/// <para>
/// Delivery is <b>exactly once per Consumer</b>: each applies the event in a transaction of its own,
/// and one Consumer failing neither repeats nor holds back another. Not a mediator notification, so
/// an <c>INotificationHandler</c> for one does not compile.
/// </para>
/// </remarks>
public interface IIntegrationEvent
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

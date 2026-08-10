using System.ComponentModel.DataAnnotations.Schema;

using ModulithTemplate.Domain.Common.Events;

namespace ModulithTemplate.Domain.Common.Entities;

/// <summary>
/// Base class for an aggregate root — the one entity in an aggregate that outside code holds a
/// reference to, and the only place domain events are raised.
/// </summary>
/// <remarks>
/// Deriving is opt-in: an entity that raises no events does not need this type. Events buffered here
/// are collected and published by the host's dispatch interceptor during
/// <c>SaveChangesAsync</c>, then cleared, so they fire exactly once per save.
/// </remarks>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// The domain events raised on this aggregate and not yet dispatched.
    /// </summary>
    /// <remarks>
    /// Marked <see cref="NotMappedAttribute"/> so EF Core treats it as behaviour rather than state
    /// and never attempts to map it. Exposed as a read-only view, so the only way to add an event is
    /// <see cref="RaiseDomainEvent"/>.
    /// </remarks>
    [NotMapped]
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Records that something domain-significant happened to this aggregate.
    /// </summary>
    /// <remarks>
    /// Call this from the mutator method that performed the change, after the invariants have been
    /// checked — an event describes something that <i>did</i> happen, so it must never be raised on
    /// a path that can still fail.
    /// </remarks>
    /// <param name="domainEvent">The event describing what happened.</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Discards the buffered events.
    /// </summary>
    /// <remarks>
    /// Called by the dispatcher immediately <i>before</i> publishing, never by feature code. Clearing
    /// first is what stops a handler that triggers another save from re-dispatching events that are
    /// already in flight.
    /// </remarks>
    public void ClearDomainEvents() => _domainEvents.Clear();
}

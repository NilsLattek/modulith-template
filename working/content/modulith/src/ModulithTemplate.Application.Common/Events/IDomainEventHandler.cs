using Mediator;

using ModulithTemplate.Domain.Common.Events;

namespace ModulithTemplate.Application.Common.Events;

/// <summary>
/// Reacts to a domain event raised inside the same feature.
/// </summary>
/// <remarks>
/// Implementations live in the raising feature's Application layer, under
/// <c>Application/DomainEventHandlers/</c>, and must be <c>public</c> for the same reason as
/// <see cref="IIntegrationEventHandler{TEvent}"/>: the mediator's source generator emits a hard
/// <c>typeof(...)</c> reference into the host's compilation.
/// <para>
/// A handler runs during <c>SaveChangesAsync</c>, so anything it writes through the same
/// <c>DbContext</c> is saved in that same call and commits atomically with the change that raised
/// the event. Throwing therefore rolls the whole operation back — which is the point.
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The domain event this handler reacts to.</typeparam>
public interface IDomainEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent;

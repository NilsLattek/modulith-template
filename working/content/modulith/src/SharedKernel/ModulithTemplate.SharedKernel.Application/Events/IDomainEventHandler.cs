using Mediator;

using ModulithTemplate.SharedKernel.Domain.Events;

namespace ModulithTemplate.SharedKernel.Application.Events;

/// <summary>
/// Reacts to a domain event raised inside the same feature.
/// </summary>
/// <remarks>
/// Implementations live under the raising feature's <c>Application/DomainEventHandlers/</c> and must
/// be <c>public</c>: the mediator's source generator emits a hard <c>typeof(...)</c> reference into
/// the host's compilation, so an <c>internal</c> handler breaks the host build with <c>CS0122</c>.
/// <para>
/// A handler runs during <c>SaveChangesAsync</c>, so its writes through the same <c>DbContext</c>
/// commit atomically with the change that raised the event — and throwing rolls the whole operation
/// back, which is the point.
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The domain event this handler reacts to.</typeparam>
public interface IDomainEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent;

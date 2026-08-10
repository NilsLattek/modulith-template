using Mediator;

namespace ModulithTemplate.Application.Common.Events;

/// <summary>
/// Marks a fact that one feature publishes for other features to react to.
/// </summary>
/// <remarks>
/// An integration event is a <b>published contract</b>, not an internal detail: it lives in its
/// feature's <c>Contracts</c> project, carries only primitives and identifiers — never an entity, a
/// value object or a <c>Result</c> — and changes additively. A breaking change means a new
/// <c>*V2IntegrationEvent</c> type alongside the old one, never an edit to the existing shape,
/// because every consumer is compiled against it.
/// <para>
/// Do not publish one of these directly. Enqueue it on <see cref="IIntegrationEventQueue"/> and let
/// the host dispatch it once the handler's work has committed.
/// </para>
/// <para>
/// The base <see cref="INotification"/> is what makes dispatch work and is the one place feature
/// code touches the mediator's notification types; handlers implement
/// <see cref="IIntegrationEventHandler{TEvent}"/> instead.
/// </para>
/// </remarks>
public interface IIntegrationEvent : INotification;

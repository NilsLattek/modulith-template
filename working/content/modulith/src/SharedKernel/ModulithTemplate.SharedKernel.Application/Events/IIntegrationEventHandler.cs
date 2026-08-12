using Mediator;

namespace ModulithTemplate.SharedKernel.Application.Events;

/// <summary>
/// Reacts to an integration event published by another feature.
/// </summary>
/// <remarks>
/// Implementations live in the <b>consuming</b> feature's Application layer, under
/// <c>Application/IntegrationEventHandlers/</c>, and must be <c>public</c>: the mediator's source
/// generator emits a hard <c>typeof(...)</c> reference into the host's compilation, so an
/// <c>internal</c> handler breaks the host build with <c>CS0122</c>.
/// <para>
/// A handler runs in the publishing message's DI scope but writes through its own feature's
/// <c>DbContext</c>, so its work commits in a <b>separate</b> transaction from the producer's. Treat
/// the reaction as eventually consistent and keep it idempotent where that matters.
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The integration event this handler reacts to.</typeparam>
public interface IIntegrationEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IIntegrationEvent;

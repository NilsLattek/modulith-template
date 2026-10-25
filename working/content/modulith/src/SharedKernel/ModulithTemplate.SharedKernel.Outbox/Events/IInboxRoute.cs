using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>A feature context that may consume Integration Events through the shared inbox.</summary>
/// <remarks>
/// One per feature context, registered by <c>AddModuleDbContext</c>. Whether it consumes a given event
/// is decided by whether a handler for its envelope was registered, not here.
/// </remarks>
public interface IInboxRoute
{
    /// <summary>The Consumer's name, which prefixes the Group key of each of its rows.</summary>
    string Consumer { get; }

    /// <summary>Addresses an event to this Consumer.</summary>
    /// <typeparam name="TEvent">The event's type.</typeparam>
    /// <param name="integrationEvent">The event.</param>
    /// <returns>This Consumer's <see cref="InboxEnvelope{TConsumerContext, TEvent}"/> for it.</returns>
    object Wrap<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent;
}

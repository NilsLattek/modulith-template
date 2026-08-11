using Microsoft.Extensions.Logging;

namespace ModulithTemplate.Infrastructure.Common.Events;

/// <summary>
/// Source-generated log messages emitted at the two event dispatch seams.
/// </summary>
/// <remarks>
/// Kept non-generic and separate from the dispatchers themselves because the <c>[LoggerMessage]</c>
/// generator does not support generic containing types. Both dispatch points log the concrete
/// event's runtime type name — <see cref="IntegrationEventQueue"/> as it flushes each queued event,
/// and <see cref="DomainEventDispatcher"/> as it publishes each buffered one.
/// <para>
/// This is where event-dispatch observability lives. Notifications are published through
/// <c>IPublisher.Publish</c>, which does not run the mediator's pipeline behaviours, so the host's
/// <c>LoggingBehaviour</c> never sees an event — logging it here is the only record that one was
/// dispatched.
/// </para>
/// </remarks>
internal static partial class EventLog
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Published integration event {EventType}")]
    public static partial void IntegrationEventPublished(ILogger logger, string eventType);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Dispatched domain event {EventType}")]
    public static partial void DomainEventDispatched(ILogger logger, string eventType);
}

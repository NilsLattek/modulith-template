namespace ModulithTemplate.Web.Events;

/// <summary>
/// Source-generated log messages for the catch-all event handlers.
/// </summary>
/// <remarks>
/// Kept non-generic and separate from the handlers themselves, for the same reason as
/// <see cref="Behaviours.BehaviourLog"/>: the <c>[LoggerMessage]</c> generator does not support
/// generic containing types, and both handlers are open generics.
/// </remarks>
internal static partial class EventLog
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Published integration event {EventType}")]
    public static partial void IntegrationEventPublished(ILogger logger, string eventType);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Dispatched domain event {EventType}")]
    public static partial void DomainEventDispatched(ILogger logger, string eventType);
}

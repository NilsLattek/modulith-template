using Microsoft.Extensions.Logging;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Source-generated log messages emitted at the domain event dispatch seam.
/// </summary>
/// <remarks>
/// Kept separate from the dispatcher because the <c>[LoggerMessage]</c> generator does not support
/// generic containing types. This is the <i>only</i> record that an event was dispatched:
/// <c>IPublisher.Publish</c> does not run the mediator's pipeline behaviours, so the host's
/// <c>LoggingBehaviour</c> never sees one.
/// </remarks>
internal static partial class EventLog
{
    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Dispatched domain event {EventType}")]
    public static partial void DomainEventDispatched(ILogger logger, string eventType);
}

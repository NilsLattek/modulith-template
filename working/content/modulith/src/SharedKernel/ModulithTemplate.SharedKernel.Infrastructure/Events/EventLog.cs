using Microsoft.Extensions.Logging;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Source-generated log messages emitted at the domain event dispatch seam.
/// </summary>
/// <remarks>
/// Kept non-generic and separate from the dispatcher itself because the <c>[LoggerMessage]</c>
/// generator does not support generic containing types. <see cref="DomainEventDispatcher"/> logs
/// each buffered event's concrete runtime type name as it publishes it.
/// <para>
/// This is where event-dispatch observability lives. Notifications are published through
/// <c>IPublisher.Publish</c>, which does not run the mediator's pipeline behaviours, so the host's
/// <c>LoggingBehaviour</c> never sees an event — logging it here is the only record that one was
/// dispatched.
/// </para>
/// </remarks>
internal static partial class EventLog
{
    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Dispatched domain event {EventType}")]
    public static partial void DomainEventDispatched(ILogger logger, string eventType);
}

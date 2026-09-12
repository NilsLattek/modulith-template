using Microsoft.Extensions.Logging;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>
/// Source-generated log messages emitted at the republish seam.
/// </summary>
internal static partial class IntegrationEventLog
{
    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Warning,
        Message = "Integration event {EventType} {EventId} has failed {RetryCount} times and is holding up group {GroupKey}")]
    public static partial void RepeatedFailure(
        ILogger logger, string eventType, Guid eventId, int retryCount, string groupKey);
}

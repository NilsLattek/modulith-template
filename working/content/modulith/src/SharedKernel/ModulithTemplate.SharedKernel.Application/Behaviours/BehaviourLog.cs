using Microsoft.Extensions.Logging;

namespace ModulithTemplate.SharedKernel.Application.Behaviours;

/// <summary>
/// Source-generated log messages for the mediator pipeline behaviours.
/// </summary>
/// <remarks>
/// Kept non-generic and separate from the behaviours themselves: the <c>[LoggerMessage]</c>
/// generator does not support generic containing types, and both behaviours are open generics.
/// Each method therefore takes the <see cref="ILogger"/> as its first parameter.
/// </remarks>
internal static partial class BehaviourLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Handling {MessageType}")]
    public static partial void Handling(ILogger logger, string messageType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Handled {MessageType} in {ElapsedMilliseconds} ms")]
    public static partial void Handled(ILogger logger, string messageType, long elapsedMilliseconds);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "{MessageType} failed in {ElapsedMilliseconds} ms: {Codes}")]
    public static partial void Failed(ILogger logger, string messageType, long elapsedMilliseconds, string codes);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "{MessageType} threw an unhandled exception (trace {TraceId})")]
    public static partial void HandlerThrew(ILogger logger, string messageType, string? traceId, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "{MessageType} was rejected in {ElapsedMilliseconds} ms: {Codes}")]
    public static partial void Rejected(ILogger logger, string messageType, long elapsedMilliseconds, string codes);
}

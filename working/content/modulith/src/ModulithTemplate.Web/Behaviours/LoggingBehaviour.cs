using System.Diagnostics;

using FluentResults;

using Mediator;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Times every message passing through the mediator and logs its outcome.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// Deliberately unconstrained on <typeparamref name="TResponse"/>, so that every message is
/// logged regardless of what its handler returns — logging must never be conditional on the
/// shape of a response. Responses that happen to be results are inspected at runtime so a
/// failed result is reported at warning rather than information.
/// </remarks>
internal sealed class LoggingBehaviour<TMessage, TResponse>(
    ILogger<LoggingBehaviour<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage).Name;
        BehaviourLog.Handling(logger, messageType);

        var startedAt = Stopwatch.GetTimestamp();
        var response = await next(message, cancellationToken);
        var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (response is IResultBase { IsFailed: true } failed)
        {
            BehaviourLog.Failed(logger, messageType, elapsedMilliseconds, string.Join("; ", failed.Errors.Select(error => error.Message)));
        }
        else
        {
            BehaviourLog.Handled(logger, messageType, elapsedMilliseconds);
        }

        return response;
    }
}

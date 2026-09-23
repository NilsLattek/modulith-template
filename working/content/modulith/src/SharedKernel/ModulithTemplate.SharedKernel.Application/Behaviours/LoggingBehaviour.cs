using System.Diagnostics;

using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;

using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.SharedKernel.Application.Behaviours;

/// <summary>
/// Traces and times every message passing through the mediator, and logs its outcome.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
public sealed class LoggingBehaviour<TMessage, TResponse>(
    ILogger<LoggingBehaviour<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    // Subscribed to by ConfigureOpenTelemetry's "ModulithTemplate.*" prefix, not by name.
    private static readonly ActivitySource ActivitySource = new("ModulithTemplate.Mediator");

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage).Name;

        using var activity = ActivitySource.StartActivity(messageType);
        activity?.SetTag("mediator.message.type", typeof(TMessage).FullName);

        BehaviourLog.Handling(logger, messageType);

        var startedAt = Stopwatch.GetTimestamp();
        var response = await next(message, cancellationToken);
        var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (response is IResultBase { IsFailed: true } failed)
        {
            var codes = string.Join(", ", failed.Errors.Select(CodeOf));

            if (failed.Errors.All(IsExpected))
            {
                BehaviourLog.Rejected(logger, messageType, elapsedMilliseconds, codes);
            }
            else
            {
                BehaviourLog.Failed(logger, messageType, elapsedMilliseconds, codes);
                activity?.SetStatus(ActivityStatusCode.Error, codes);
            }
        }
        else
        {
            BehaviourLog.Handled(logger, messageType, elapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        return response;
    }

    private static bool IsExpected(IError error) => error is AppError and not UnexpectedError;

    // An error from outside the taxonomy has no code; its type name is the safe stand-in.
    private static string CodeOf(IError error) => error is AppError appError ? appError.Code : error.GetType().Name;
}

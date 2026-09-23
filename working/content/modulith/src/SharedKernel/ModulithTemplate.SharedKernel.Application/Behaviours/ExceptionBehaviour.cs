using System.Diagnostics;

using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;

using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.SharedKernel.Application.Behaviours;

/// <summary>
/// Converts an unhandled exception thrown by a message handler into a failed result of the
/// handler's own response type, so callers branch on <c>IsFailed</c> instead of catching.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// The <typeparamref name="TResponse"/> constraint makes the conversion type-safe — FluentResults
/// parameterises each result type by itself, so <c>WithError</c> returns the concrete type it was
/// called on — and also decides which messages are covered at all: a handler returning anything
/// other than a result is not wrapped, and its exceptions propagate.
/// <para>
/// An exception an <see cref="IExceptionTranslator"/> recognises becomes the error it names; any
/// other is logged with its trace id and becomes an <see cref="UnexpectedError"/> carrying only that
/// id, so exception text never reaches the caller.
/// </para>
/// </remarks>
public sealed class ExceptionBehaviour<TMessage, TResponse>(
    IEnumerable<IExceptionTranslator> translators,
    ILogger<ExceptionBehaviour<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : ResultBase<TResponse>, new()
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a failure — let the caller observe it as cancellation.
            throw;
        }
        catch (Exception ex)
        {
            foreach (var translator in translators)
            {
                if (translator.Translate(ex) is { } expected)
                {
                    return new TResponse().WithError(expected);
                }
            }

            var traceId = Activity.Current?.TraceId.ToHexString();
            BehaviourLog.HandlerThrew(logger, typeof(TMessage).Name, traceId, ex);
            return new TResponse().WithError(new UnexpectedError(traceId));
        }
    }
}

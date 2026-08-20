using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;

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
/// </remarks>
public sealed class ExceptionBehaviour<TMessage, TResponse>(
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
            BehaviourLog.HandlerThrew(logger, typeof(TMessage).Name, ex);
            return new TResponse().WithError(new ExceptionalError(ex));
        }
    }
}

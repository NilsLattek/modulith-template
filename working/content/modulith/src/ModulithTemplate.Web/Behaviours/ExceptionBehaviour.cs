using FluentResults;

using Mediator;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Converts an unhandled exception thrown by a message handler into a failed result of the
/// handler's own response type, so callers branch on <c>IsFailed</c> instead of catching.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// The <typeparamref name="TResponse"/> constraint is what makes the conversion type-safe:
/// FluentResults parameterises each result type by itself (<c>Result : ResultBase&lt;Result&gt;</c>,
/// <c>Result&lt;T&gt; : ResultBase&lt;Result&lt;T&gt;&gt;</c>), so <c>WithError</c> returns the
/// concrete type it was called on. It also decides which messages this behaviour applies to at
/// all — a handler returning something other than a result is not wrapped, and its exceptions
/// propagate. That is why every handler in this solution returns <c>Result</c> or
/// <c>Result&lt;T&gt;</c>.
/// </remarks>
internal sealed class ExceptionBehaviour<TMessage, TResponse>(
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

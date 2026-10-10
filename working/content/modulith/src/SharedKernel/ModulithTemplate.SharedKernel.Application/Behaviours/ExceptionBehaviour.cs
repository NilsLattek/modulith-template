using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;

using ModulithTemplate.SharedKernel.Application.Errors;
using ModulithTemplate.SharedKernel.Domain.Exceptions;

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
/// A <see cref="BusinessException"/> is an expected outcome, not a fault: it becomes a
/// <see cref="BusinessError"/> without an error log, and <c>LoggingBehaviour</c> records it as a
/// failed result. A <see cref="ConcurrencyException"/> is expected too, and becomes a
/// <see cref="ConcurrencyError"/>.
/// </para>
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
        catch (BusinessException ex)
        {
            return new TResponse().WithError(new BusinessError(ex));
        }
        catch (ConcurrencyException)
        {
            return new TResponse().WithError(new ConcurrencyError());
        }
        catch (Exception ex)
        {
            BehaviourLog.HandlerThrew(logger, typeof(TMessage).Name, ex);
            return new TResponse().WithError(new ExceptionalError(ex));
        }
    }
}

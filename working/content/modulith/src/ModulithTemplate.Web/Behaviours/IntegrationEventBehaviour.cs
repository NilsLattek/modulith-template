using FluentResults;

using Mediator;

using ModulithTemplate.Application.Common.Events;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Publishes the integration events a handler enqueued, but only once that handler has returned a
/// successful result — so a consumer never reacts to work that failed or was never committed.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// Registered as the <b>innermost</b> behaviour, which is what makes the ordering work:
/// <c>Logging → Exception → Validation → IntegrationEvent → handler</c>. Inside
/// <see cref="ValidationBehaviour{TMessage, TResponse}"/>, so an invalid message never publishes;
/// inside <see cref="ExceptionBehaviour{TMessage, TResponse}"/>, so a consumer that throws becomes a
/// failed result rather than an unhandled exception.
/// <para>
/// The <typeparamref name="TResponse"/> constraint mirrors the sibling behaviours: dispatch is
/// decided by <c>IsSuccess</c>, so a handler returning something other than a result is not
/// dispatched for at all. Every handler in this solution returns a result, which is what keeps that
/// from being a silent hole.
/// </para>
/// <para>
/// <b>The trade this makes.</b> Each feature owns its own <c>DbContext</c>, so a consumer commits in
/// a different transaction from the producer. Publishing here — after the handler's write, before
/// the response is returned — gives at-most-once delivery: if the process dies in between, the event
/// is lost, and if a consumer throws, the producer's write still stands while the caller sees a
/// failed result. That is the accepted cost of not running an outbox; see the queue's remarks for
/// the upgrade path.
/// </para>
/// </remarks>
/// <param name="queue">The scoped queue holding this message's buffered events.</param>
internal sealed class IntegrationEventBehaviour<TMessage, TResponse>(IIntegrationEventQueue queue)
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
        var response = await next(message, cancellationToken);

        if (response.IsSuccess)
        {
            await queue.FlushAsync(cancellationToken);
        }

        return response;
    }
}

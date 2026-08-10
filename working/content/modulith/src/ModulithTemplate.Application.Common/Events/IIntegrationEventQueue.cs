namespace ModulithTemplate.Application.Common.Events;

/// <summary>
/// Buffers the integration events raised while a message is handled, so the host can publish them
/// once the handler has succeeded.
/// </summary>
/// <remarks>
/// Handlers enqueue; they never publish. Deferring the dispatch is what keeps a consumer from ever
/// observing a write that has not committed — or, worse, one that was rolled back.
/// <para>
/// This interface is also the single seam through which events leave a feature. Swapping the
/// in-process dispatch for a transactional outbox or a real broker means replacing one
/// implementation, with no change to any feature.
/// </para>
/// </remarks>
public interface IIntegrationEventQueue
{
    /// <summary>
    /// Buffers <paramref name="integrationEvent"/> for dispatch after the current message succeeds.
    /// </summary>
    /// <param name="integrationEvent">The event to publish once the handler's work has committed.</param>
    void Enqueue(IIntegrationEvent integrationEvent);

    /// <summary>
    /// Publishes every buffered event and empties the queue.
    /// </summary>
    /// <remarks>
    /// Called by the host's integration-event pipeline behaviour, not by feature code. Draining
    /// continues until the queue is empty, so a consumer may enqueue its own event and see it
    /// dispatched within the same flush.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    ValueTask FlushAsync(CancellationToken cancellationToken);
}

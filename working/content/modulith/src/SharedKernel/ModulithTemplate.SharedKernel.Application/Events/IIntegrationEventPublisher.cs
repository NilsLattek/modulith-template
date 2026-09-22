namespace ModulithTemplate.SharedKernel.Application.Events;

/// <summary>
/// Records an integration event for delivery, in the caller's own unit of work.
/// </summary>
/// <remarks>
/// A feature injects its own <c>I&lt;Name&gt;IntegrationEventPublisher</c> marker rather than this,
/// which is what binds the registration to that feature's <c>DbContext</c> — the same reason
/// <c>I&lt;Name&gt;Repository&lt;T&gt;</c> exists.
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>Stages the event, to be written by the caller's next save.</summary>
    /// <remarks>
    /// Synchronous and without a cancellation token because it performs no I/O: it only adds a row
    /// to the change tracker. Nothing is delivered until the save carrying the business change
    /// commits, and a rollback takes the event with it.
    /// </remarks>
    /// <param name="integrationEvent">The event to record.</param>
    void Publish(IIntegrationEvent integrationEvent);
}

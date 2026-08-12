using Mediator;

using Microsoft.Extensions.Logging;

using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// The in-process <see cref="IIntegrationEventQueue"/>: buffers events for the lifetime of one DI
/// scope and publishes them through the mediator when the host flushes.
/// </summary>
/// <remarks>
/// Registered scoped, so each message gets its own buffer and one request's events can never leak
/// into another's.
/// <para>
/// This is the single place integration events leave a feature. Replacing it with a transactional
/// outbox — persist on <see cref="Enqueue"/>, publish from a background worker — is a change to this
/// class alone.
/// </para>
/// </remarks>
/// <param name="publisher">The mediator's notification publisher.</param>
/// <param name="logger">Records each event as it is dispatched.</param>
internal sealed class IntegrationEventQueue(
    IPublisher publisher,
    ILogger<IntegrationEventQueue> logger) : IIntegrationEventQueue
{
    /// <summary>
    /// How many times <see cref="FlushAsync"/> will re-drain before assuming a publish cycle.
    /// </summary>
    /// <remarks>
    /// A handler may enqueue an event of its own, which is legitimate and drains in the same flush.
    /// Two handlers that enqueue each other's events would otherwise loop forever, and a hung
    /// request is far harder to diagnose than a thrown exception — hence the bound.
    /// </remarks>
    private const int MaxDrainIterations = 100;

    private readonly Queue<IIntegrationEvent> _pending = new();

    /// <inheritdoc />
    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _pending.Enqueue(integrationEvent);
    }

    /// <inheritdoc />
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        var drained = 0;

        while (_pending.TryDequeue(out var integrationEvent))
        {
            if (++drained > MaxDrainIterations)
            {
                throw new InvalidOperationException(
                    $"Integration event dispatch exceeded {MaxDrainIterations} events in a single flush, " +
                    $"most recently '{integrationEvent.GetType().Name}'. Two handlers are most likely " +
                    "publishing events that trigger each other.");
            }

            // The object overload, deliberately. Both of the mediator's Publish overloads switch on
            // the *runtime* type, so either would dispatch correctly — but the generic one reads as
            // though it keyed on IIntegrationEvent, which is not what happens.
            //
            // That switch only covers notification types the source generator found in the host's
            // compilation, and its default branch neither dispatches nor throws. An event whose
            // Contracts project the host does not reference is therefore dropped in silence, which
            // is why registering every feature with the host is not optional.
            EventLog.IntegrationEventPublished(logger, integrationEvent.GetType().Name);
            await publisher.Publish((object)integrationEvent, cancellationToken);
        }
    }
}

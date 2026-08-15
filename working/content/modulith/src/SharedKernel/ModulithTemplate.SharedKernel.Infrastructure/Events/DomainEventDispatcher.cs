using Mediator;

using Microsoft.Extensions.Logging;

using ModulithTemplate.SharedKernel.Domain.Entities;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Publishes the domain events buffered on tracked aggregates, repeating until none remain.
/// </summary>
/// <remarks>
/// Split out of <see cref="DomainEventDispatchInterceptor{TContext}"/> so the ordering guarantees below can be
/// asserted without a live database: the interceptor is the EF Core adapter, this is the behaviour.
/// </remarks>
/// <param name="publisher">The mediator's notification publisher.</param>
/// <param name="logger">Records each event as it is dispatched.</param>
public sealed class DomainEventDispatcher(
    IPublisher publisher,
    ILogger<DomainEventDispatcher> logger)
{
    /// <summary>
    /// How many collect-and-publish rounds are allowed before a handler cycle is assumed.
    /// </summary>
    private const int MaxDispatchRounds = 10;

    /// <summary>
    /// Publishes every buffered domain event, including any raised by the handlers themselves.
    /// </summary>
    /// <remarks>
    /// Two orderings carry the correctness of this method:
    /// <list type="bullet">
    /// <item><description>
    /// Events are <b>cleared before</b> they are published. A handler that triggers another save
    /// would otherwise find them still buffered and dispatch them a second time.
    /// </description></item>
    /// <item><description>
    /// Aggregates are <b>re-collected after</b> each round, because a handler may raise further
    /// events — or add an entirely new aggregate — while running.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// <param name="trackedAggregates">
    /// Returns the aggregates currently tracked. Called once per round, so it must re-read the
    /// change tracker rather than close over a snapshot.
    /// </param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <exception cref="InvalidOperationException">
    /// Handlers kept raising events for <see cref="MaxDispatchRounds"/> rounds, which means a cycle.
    /// </exception>
    public async ValueTask DispatchAsync(
        Func<IReadOnlyList<AggregateRoot>> trackedAggregates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trackedAggregates);

        for (var round = 0; round < MaxDispatchRounds; round++)
        {
            var raising = trackedAggregates().Where(aggregate => aggregate.DomainEvents.Count > 0).ToList();
            if (raising.Count == 0)
            {
                return;
            }

            var dispatching = raising.SelectMany(aggregate => aggregate.DomainEvents).ToList();
            foreach (var aggregate in raising)
            {
                aggregate.ClearDomainEvents();
            }

            foreach (var domainEvent in dispatching)
            {
                // The object overload dispatches on the runtime type; see IntegrationEventQueue for
                // why that matters and what happens to a type the host's generator never saw.
                EventLog.DomainEventDispatched(logger, domainEvent.GetType().Name);
                await publisher.Publish((object)domainEvent, cancellationToken);
            }
        }

        // Only reachable when every round still found events to dispatch.
        throw new InvalidOperationException(
            $"Domain event dispatch did not settle after {MaxDispatchRounds} rounds. Handlers are " +
            "most likely raising events that trigger each other.");
    }
}

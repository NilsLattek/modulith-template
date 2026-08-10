using Microsoft.EntityFrameworkCore.Diagnostics;

using ModulithTemplate.Domain.Common.Entities;

namespace ModulithTemplate.Infrastructure.Common.Events;

/// <summary>
/// Dispatches domain events during <c>SaveChangesAsync</c>, so handlers write through the same
/// <c>DbContext</c> and commit in the same transaction as the change that raised them.
/// </summary>
/// <remarks>
/// This is the difference between the two kinds of event. An integration event crosses features,
/// where each owns its own context, so it is published only <i>after</i> the producer commits and is
/// eventually consistent. A domain event stays inside one feature and therefore inside one
/// transaction, so it is dispatched <i>before</i> the save completes and its handler's writes are
/// part of that same save. A handler that throws rolls the whole operation back, which is the point.
/// <para>
/// <b>Only asynchronous saves dispatch.</b> Publishing is asynchronous, so there is no correct way
/// to do it from the synchronous <c>SaveChanges</c> path. Every save in this solution goes through
/// <c>SaveChangesAsync</c> — the repositories and handlers are async throughout — but a hand-written
/// synchronous save would silently raise no events.
/// </para>
/// </remarks>
/// <param name="dispatcher">Performs the collect-clear-publish rounds.</param>
public sealed class DomainEventDispatchInterceptor(DomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    private bool _dispatching;

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        // Re-entrancy guard: a handler that calls SaveChangesAsync on this context lands back here
        // mid-dispatch. The outer loop is already re-collecting the change tracker each round, so
        // the nested call must not start a competing dispatch of its own.
        if (eventData.Context is null || _dispatching)
        {
            return result;
        }

        _dispatching = true;
        try
        {
            await dispatcher.DispatchAsync(
                () => eventData.Context.ChangeTracker.Entries<AggregateRoot>()
                    .Select(entry => entry.Entity)
                    .ToList(),
                cancellationToken);
        }
        finally
        {
            _dispatching = false;
        }

        return result;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using ModulithTemplate.SharedKernel.Domain.Entities;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

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
/// <para>
/// <b>Generic in the context type on purpose.</b> The re-entrancy guard below is instance state, so
/// an instance must serve exactly one context. Closing it over <typeparamref name="TContext"/> gives
/// each feature its own registration — and therefore its own guard — instead of one shared instance
/// on which a handler saving feature B's context would look like re-entrancy on feature A's and
/// silently skip B's events.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The feature's context type; only saves on it are dispatched.</typeparam>
/// <param name="dispatcher">Performs the collect-clear-publish rounds.</param>
public sealed class DomainEventDispatchInterceptor<TContext>(DomainEventDispatcher dispatcher)
    : SaveChangesInterceptor
    where TContext : DbContext
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
        if (eventData.Context is not TContext context || _dispatching)
        {
            return result;
        }

        _dispatching = true;
        try
        {
            await dispatcher.DispatchAsync(
                () => context.ChangeTracker.Entries<AggregateRoot>()
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using ModulithTemplate.SharedKernel.Domain.Entities;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Dispatches domain events during <c>SaveChangesAsync</c>, so handlers write through the same
/// <c>DbContext</c> and commit in the same transaction as the change that raised them.
/// </summary>
/// <remarks>
/// Dispatching inside the save is what makes a domain event safe without any delivery machinery:
/// nothing is buffered for later, and a handler that throws rolls the whole operation back.
/// <para>
/// <b>Only asynchronous saves dispatch</b>, because publishing is asynchronous. Everything here is
/// async throughout, but a hand-written synchronous <c>SaveChanges</c> would silently raise no
/// events.
/// </para>
/// <para>
/// <b>Generic in the context type on purpose</b>: the re-entrancy guard below is instance state, so
/// each feature needs its own registration. With one shared instance, a handler saving feature B's
/// context would look like re-entrancy on feature A's and silently skip B's events.
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

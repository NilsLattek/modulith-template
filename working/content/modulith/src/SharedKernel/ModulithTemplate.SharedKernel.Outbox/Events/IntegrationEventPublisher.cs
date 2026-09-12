using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>
/// Stages a feature's integration events into the shared outbox using that feature's own context.
/// </summary>
/// <remarks>
/// Derived from the way <c>RepositoryBase&lt;T&gt;</c> is: a feature declares a body-less
/// <c>&lt;Name&gt;IntegrationEventPublisher</c> binding its marker interface to its own context, so
/// the row rides that feature's save rather than a sibling's.
/// <para>
/// Bound in the feature's <c>Web</c> composition root rather than beside the repository: the marker
/// interface lives in <c>Application</c>, which a feature's <c>Infrastructure</c> may not depend on.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The feature's context, which the row is written through.</typeparam>
/// <param name="dbContext">The feature's context.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
public abstract class IntegrationEventPublisher<TContext>(TContext dbContext, IOutbox outbox)
    : IIntegrationEventPublisher
    where TContext : DbContext, IOutboxDbContext
{
    /// <inheritdoc />
    public void Publish(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(outbox);

        // Staged, never added: AddMessageAsync saves and expects a transaction the caller opened,
        // which would separate the event from the change that caused it. This row is written by
        // whichever SaveChanges comes next — the one persisting the aggregate.
        outbox.StageMessage(
            dbContext,
            new OutboxMessage(
                integrationEvent.EventId,
                DateTime.UtcNow,
                integrationEvent,
                integrationEvent.GroupKey));
    }
}

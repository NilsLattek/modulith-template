using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>
/// Stages a feature's integration events, using that feature's own context: one inbox row per Consumer,
/// and an outbox row when the event is forwarded outside the process.
/// </summary>
/// <remarks>
/// Derived from the way <c>FeatureRepository&lt;T&gt;</c> is: a feature declares a body-less
/// <c>&lt;Name&gt;IntegrationEventPublisher</c> binding its marker interface to its own context, so
/// the rows ride that feature's save rather than a sibling's.
/// </remarks>
/// <typeparam name="TContext">The feature's context, which the rows are written through.</typeparam>
/// <param name="dbContext">The feature's context.</param>
/// <param name="inbox">The inbox the Consumers' rows are staged in.</param>
/// <param name="outbox">The outbox a forwarded event is staged in.</param>
/// <param name="subscriptions">Who receives each event.</param>
public abstract class IntegrationEventPublisher<TContext>(
    TContext dbContext, IInbox inbox, IOutbox outbox, IntegrationEventSubscriptions subscriptions)
    : IIntegrationEventPublisher
    where TContext : DbContext, IInboxDbContext, IOutboxDbContext
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">No Consumer handles the event and nothing forwards it.</exception>
    public void Publish<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent
    {
        var createdAt = DateTime.UtcNow;
        var rows = subscriptions.InboxRows(integrationEvent, createdAt);
        // TEvent, never the runtime type, for both: the envelope a Consumer's handler is registered
        // under is built from it, so the two decisions cannot disagree.
        var forwarded = subscriptions.IsForwarded<TEvent>();

        if (rows.Count == 0 && !forwarded)
        {
            throw new InvalidOperationException(
                $"Nothing receives {typeof(TEvent).Name}. A consuming feature declares an "
                    + "IntegrationEventInboxTransport for it under Infrastructure/InboxHandlers/; forwarding it "
                    + "outside the process takes an IOutboxMessageHandler.");
        }

        // Staged, never added: AddMessageAsync saves and expects a transaction the caller opened,
        // which would separate the event from the change that caused it. These rows are written by
        // whichever SaveChanges comes next — the one persisting the aggregate.
        inbox.StageMessages(dbContext, rows);
        if (forwarded)
        {
            outbox.StageMessage(
                dbContext,
                new OutboxMessage(integrationEvent.EventId, createdAt, integrationEvent, integrationEvent.GroupKey));
        }
    }
}

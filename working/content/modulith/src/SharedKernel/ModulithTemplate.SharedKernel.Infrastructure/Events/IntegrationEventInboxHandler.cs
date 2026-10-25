using FluentResults;

using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.SharedKernel.Outbox.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Npgsql;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// One Consumer's reaction to an Integration Event: adds the Consumer's own inbox row when the event
/// is published, then applies that row exactly once by sending one of the Consumer's Commands.
/// </summary>
/// <remarks>
/// A Consumer derives one class per event under <c>Infrastructure/InboxHandlers/</c>, overriding
/// <see cref="ToCommand"/>. The mediator generator registers it as a notification handler, and the
/// outbox generator as the inbox handler for its envelope.
/// </remarks>
/// <typeparam name="TEvent">The event consumed.</typeparam>
/// <typeparam name="TContext">The consuming feature's context.</typeparam>
/// <param name="inbox">Adds the Consumer's row.</param>
/// <param name="inboxContext">The shared inbox's context, in the scope the message is processed in.</param>
/// <param name="scopeFactory">Opens the scope the Command runs in.</param>
public abstract class IntegrationEventInboxHandler<TEvent, TContext>(
    IInbox inbox, IInboxDbContext inboxContext, IServiceScopeFactory scopeFactory)
    : INotificationHandler<TEvent>, IInboxMessageHandler<InboxEnvelope<TContext, TEvent>>
    where TEvent : IIntegrationEvent
    where TContext : DbContext
{
    /// <summary>Maps the event to the Command this feature reacts with.</summary>
    /// <param name="integrationEvent">The delivered event.</param>
    /// <returns>The Command to send.</returns>
    protected abstract ICommand<Result> ToCommand(TEvent integrationEvent);

    /// <summary>Adds this Consumer's row, unless an earlier delivery of the event already did.</summary>
    /// <param name="notification">The event, published by its outbox handler.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task completing once the row is saved.</returns>
    public async ValueTask Handle(TEvent notification, CancellationToken cancellationToken)
    {
        var envelope = new InboxEnvelope<TContext, TEvent>(notification);
        InboxMessage? row = null;

        try
        {
            await inboxContext.ExecuteInTransactionAsync(
                async token =>
                {
                    row = envelope.ToInboxMessage(DateTime.UtcNow);
                    await inbox.AddMessageAsync(inboxContext, row, token);
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            TableName: OutboxSchema.InboxTableName,
        })
        {
            // The outbox delivers at least once and the row's id is deterministic, so this Consumer's
            // row is already there. Detached, or the next Consumer's save would insert it again.
            inboxContext.InboxMessages.Entry(row!).State = EntityState.Detached;
        }
    }

    /// <summary>Applies the row: sends the Command inside the inbox's transaction.</summary>
    /// <param name="message">The claimed row's envelope.</param>
    /// <param name="metadata">The row's metadata.</param>
    /// <param name="cancellationToken">Cancels the Command.</param>
    /// <returns>A task completing once the Command has run.</returns>
    /// <exception cref="InvalidOperationException">The Command returned a failed result, so the row is retried.</exception>
    public async Task HandleAsync(
        InboxEnvelope<TContext, TEvent> message, MessageMetadata metadata, CancellationToken cancellationToken)
    {
        var transaction = inboxContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("An inbox row is handled only inside the inbox's transaction.");

        // A scope per call, not the processing scope: a retrying strategy replays the attempt in the
        // same scope, and the replay must not inherit the failed attempt's tracked entities.
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        context.Database.SetDbConnection(inboxContext.Database.GetDbConnection(), contextOwnsConnection: false);
        await context.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

        var command = ToCommand(message.Event);
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);

        // Thrown rather than returned, so the library rolls the Command back to its savepoint and
        // retries the row with backoff; a refusal is not a reason to drop the event.
        if (result.IsFailed)
        {
            throw new InvalidOperationException(
                $"{command.GetType().Name} failed for {typeof(TEvent).Name} {message.Event.EventId}: "
                    + string.Join("; ", result.Errors.Select(error => error.Message)),
                result.Errors.OfType<ExceptionalError>().FirstOrDefault()?.Exception);
        }
    }
}

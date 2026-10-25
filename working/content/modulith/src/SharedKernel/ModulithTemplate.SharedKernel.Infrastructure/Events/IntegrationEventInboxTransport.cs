using FluentResults;

using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Applies one Consumer's inbox row by sending one of the Consumer's own Commands, inside the inbox's
/// transaction, so the Command's writes and the row's completion commit together.
/// </summary>
/// <remarks>
/// A Consumer derives one class per event under <c>Infrastructure/InboxHandlers/</c>, overriding
/// <see cref="ToCommand"/>; the outbox source generator registers it under the envelope's type name.
/// </remarks>
/// <typeparam name="TEvent">The event consumed.</typeparam>
/// <typeparam name="TContext">The consuming feature's context.</typeparam>
/// <param name="services">The provider of the scope the inbox processes this message in.</param>
public abstract class IntegrationEventInboxTransport<TEvent, TContext>(IServiceProvider services)
    : IInboxMessageHandler<InboxEnvelope<TContext, TEvent>>
    where TEvent : IIntegrationEvent
    where TContext : DbContext
{
    /// <summary>Maps the event to the Command this feature reacts with.</summary>
    /// <param name="integrationEvent">The delivered event.</param>
    /// <returns>The Command to send.</returns>
    protected abstract ICommand<Result> ToCommand(TEvent integrationEvent);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The Command returned a failed result, so the row is retried.</exception>
    public async Task HandleAsync(
        InboxEnvelope<TContext, TEvent> message, MessageMetadata metadata, CancellationToken cancellationToken)
    {
        var inbox = services.GetRequiredService<IInboxDbContext>().Database;
        var transaction = inbox.CurrentTransaction
            ?? throw new InvalidOperationException("An inbox row is handled only inside the inbox's transaction.");

        // A scope per call, not the processing scope: a retrying strategy replays the attempt in the
        // same scope, and the replay must not inherit the failed attempt's tracked entities.
        await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        context.Database.SetDbConnection(inbox.GetDbConnection(), contextOwnsConnection: false);
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

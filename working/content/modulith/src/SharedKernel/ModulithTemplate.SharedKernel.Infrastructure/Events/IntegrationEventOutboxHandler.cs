using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.SharedKernel.Outbox.Data;

using Npgsql;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Infrastructure.Events;

/// <summary>
/// Takes a claimed outbox row carrying an Integration Event and publishes it in-process: each
/// Consumer's <see cref="IntegrationEventInboxHandler{TEvent, TContext}"/> stages its inbox row, and
/// any forwarder sends it outside the process. The rows are then saved together.
/// </summary>
/// <remarks>
/// A feature derives one body-less handler per event it publishes, under <c>Infrastructure/OutboxHandlers/</c>.
/// </remarks>
/// <typeparam name="TEvent">The integration event this handler delivers.</typeparam>
/// <param name="services">The provider of the scope the outbox processes this message in.</param>
public abstract class IntegrationEventOutboxHandler<TEvent>(IServiceProvider services)
    : IOutboxMessageHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    /// <inheritdoc />
    public async Task HandleAsync(TEvent message, MessageMetadata metadata, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<IPublisher>().Publish(message, cancellationToken);

        try
        {
            await services.GetRequiredService<IInboxDbContext>().SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            TableName: OutboxSchema.InboxTableName,
        })
        {
            // The outbox delivers at least once: an earlier attempt saved these rows and then lost the
            // row's completion. The ids are deterministic and saved all-or-nothing, so they are all there.
            return;
        }

        // Rows saved outside a transaction the library opened trigger no push, so start a run now.
        services.GetRequiredService<IInbox>().ProcessMessages();
    }
}

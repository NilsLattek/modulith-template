using ModulithTemplate.Application.Common.Events;
using ModulithTemplate.Domain.Common.Events;

namespace ModulithTemplate.Web.Events;

/// <summary>
/// Logs every domain event that is dispatched, whatever its type.
/// </summary>
/// <typeparam name="TEvent">Any domain event.</typeparam>
/// <remarks>
/// The domain-event counterpart of <see cref="IntegrationEventLogHandler{TEvent}"/>, and it exists
/// for the same two reasons: observability, and keeping <c>MSG0005</c> from failing the build for an
/// event that nothing has subscribed to yet.
/// </remarks>
/// <param name="logger">Logger for this handler's closed generic type.</param>
public sealed class DomainEventLogHandler<TEvent>(ILogger<DomainEventLogHandler<TEvent>> logger)
    : IDomainEventHandler<TEvent>
    where TEvent : IDomainEvent
{
    /// <inheritdoc />
    public ValueTask Handle(TEvent notification, CancellationToken cancellationToken)
    {
        EventLog.DomainEventDispatched(logger, typeof(TEvent).Name);
        return ValueTask.CompletedTask;
    }
}

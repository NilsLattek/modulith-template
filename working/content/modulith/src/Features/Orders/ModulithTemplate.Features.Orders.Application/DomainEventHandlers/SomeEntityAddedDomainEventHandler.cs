using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Domain.Events;
using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.Features.Orders.Application.DomainEventHandlers;

/// <summary>
/// Translates the internal <see cref="SomeEntityAddedDomainEvent"/> into the published
/// <see cref="SomeEntityAddedIntegrationEvent"/>.
/// </summary>
/// <remarks>
/// The one place the mapping lives, so it cannot drift between the paths that raise the event. The
/// translation is an application concern (ADR 0002): the domain model never learns that siblings or
/// a delivery mechanism exist.
/// <para>
/// It runs inside the save that persisted the aggregate, and only stages the row — so the event is
/// written by that same save, or not at all.
/// </para>
/// </remarks>
/// <param name="publisher">Stages the integration event in the Orders unit of work.</param>
public sealed class SomeEntityAddedDomainEventHandler(IOrdersIntegrationEventPublisher publisher)
    : IDomainEventHandler<SomeEntityAddedDomainEvent>
{
    /// <inheritdoc />
    public ValueTask Handle(SomeEntityAddedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        publisher.Publish(new SomeEntityAddedIntegrationEvent(
            Guid.CreateVersion7(),
            notification.SomeEntityId,
            notification.Name,
            notification.Amount));

        return ValueTask.CompletedTask;
    }
}

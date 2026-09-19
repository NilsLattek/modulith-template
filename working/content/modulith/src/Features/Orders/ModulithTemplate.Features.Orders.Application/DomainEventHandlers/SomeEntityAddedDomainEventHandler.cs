using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Domain.Events;
using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.Features.Orders.Application.DomainEventHandlers;

/// <summary>
/// Translates the internal <see cref="SomeEntityAddedDomainEvent"/> into the published
/// <see cref="SomeEntityAddedIntegrationEvent"/>.
/// </summary>
/// <param name="publisher">Stages the integration event in the Orders unit of work.</param>
public sealed class SomeEntityAddedDomainEventHandler(IOrdersIntegrationEventPublisher publisher)
    : IDomainEventHandler<SomeEntityAddedDomainEvent>
{
    /// <inheritdoc />
    public ValueTask Handle(SomeEntityAddedDomainEvent notification, CancellationToken cancellationToken)
    {
        publisher.Publish(new SomeEntityAddedIntegrationEvent(
            Guid.CreateVersion7(),
            notification.SomeEntityId,
            notification.Name,
            notification.Amount));

        return ValueTask.CompletedTask;
    }
}

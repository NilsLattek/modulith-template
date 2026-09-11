using Mediator;

using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.Features.Payments.Domain.Entities;
using ModulithTemplate.Features.Payments.Domain.Specifications;

namespace ModulithTemplate.Features.Payments.Application.IntegrationEventHandlers;

/// <summary>
/// Records a payment when Orders announces a new entity.
/// </summary>
/// <remarks>
/// An ordinary mediator notification handler: consuming introduces no concept of its own. It runs in
/// a scope of its own with its own <c>DbContext</c>, so it commits separately from the Orders
/// transaction that published the event, and it reaches nothing of Orders but its
/// <c>Contracts</c> project.
/// <para>
/// Delivery is at least once — a sibling consumer's failure re-runs this one — so the handler checks
/// first rather than assuming it has not seen this order before.
/// </para>
/// </remarks>
/// <param name="repository">The Payments feature's repository.</param>
public sealed class SomeEntityAddedIntegrationEventHandler(IPaymentsRepository<Payment> repository)
    : INotificationHandler<SomeEntityAddedIntegrationEvent>
{
    /// <inheritdoc />
    public async ValueTask Handle(
        SomeEntityAddedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (await repository.AnyAsync(new PaymentForOrderSpec(notification.SomeEntityId), cancellationToken))
        {
            return;
        }

        await repository.AddAsync(
            Payment.Record(notification.SomeEntityId, notification.Amount), cancellationToken);
    }
}

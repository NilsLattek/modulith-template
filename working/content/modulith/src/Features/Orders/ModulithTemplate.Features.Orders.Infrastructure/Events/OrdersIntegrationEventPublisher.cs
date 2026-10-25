using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Orders.Infrastructure.Events;

/// <summary>Binds the Orders publisher marker to the Orders context.</summary>
/// <param name="dbContext">The Orders context the event is staged in.</param>
/// <param name="inbox">The inbox the Consumers' rows are staged in.</param>
/// <param name="outbox">The outbox a forwarded event is staged in.</param>
/// <param name="subscriptions">Who receives each event.</param>
internal sealed class OrdersIntegrationEventPublisher(
    OrdersContext dbContext, IInbox inbox, IOutbox outbox, IntegrationEventSubscriptions subscriptions)
    : IntegrationEventPublisher<OrdersContext>(dbContext, inbox, outbox, subscriptions), IOrdersIntegrationEventPublisher;

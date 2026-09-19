using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Orders.Infrastructure.Events;

/// <summary>Binds the Orders publisher marker to the Orders context.</summary>
/// <param name="dbContext">The Orders context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class OrdersIntegrationEventPublisher(OrdersContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<OrdersContext>(dbContext, outbox), IOrdersIntegrationEventPublisher;

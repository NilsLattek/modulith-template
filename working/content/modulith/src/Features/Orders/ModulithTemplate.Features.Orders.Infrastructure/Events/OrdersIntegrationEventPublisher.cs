using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Orders.Infrastructure.Events;

/// <summary>Binds the Orders publisher marker to the Orders context.</summary>
/// <remarks>
/// Sits opposite the outbox handlers that read rows back: staging one is infrastructure too. The
/// marker it implements is an <c>Application</c> port, which this layer may name — see ADR 0004.
/// </remarks>
/// <param name="dbContext">The Orders context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class OrdersIntegrationEventPublisher(OrdersContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<OrdersContext>(dbContext, outbox), IOrdersIntegrationEventPublisher;

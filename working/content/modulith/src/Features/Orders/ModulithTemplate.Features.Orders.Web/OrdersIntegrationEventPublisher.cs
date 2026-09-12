using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Orders.Web;

/// <summary>Binds the Orders publisher marker to the Orders context.</summary>
/// <remarks>
/// Lives here rather than beside <c>OrdersRepository</c> because the marker interface is in
/// <c>Application</c>, which a feature's <c>Infrastructure</c> may not depend on — the one place
/// this mechanism's layering differs from the repository's.
/// </remarks>
/// <param name="dbContext">The Orders context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class OrdersIntegrationEventPublisher(OrdersContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<OrdersContext>(dbContext, outbox), IOrdersIntegrationEventPublisher;

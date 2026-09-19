using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Payments.Infrastructure.Events;

/// <summary>Binds the Payments publisher marker to the Payments context.</summary>
/// <remarks>
/// Sits opposite the outbox handlers that read rows back: staging one is infrastructure too. The
/// marker it implements is an <c>Application</c> port, which this layer may name — see ADR 0004.
/// </remarks>
/// <param name="dbContext">The Payments context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class PaymentsIntegrationEventPublisher(PaymentsContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<PaymentsContext>(dbContext, outbox), IPaymentsIntegrationEventPublisher;

using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Payments.Infrastructure.Events;

/// <summary>Binds the Payments publisher marker to the Payments context.</summary>
/// <param name="dbContext">The Payments context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class PaymentsIntegrationEventPublisher(PaymentsContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<PaymentsContext>(dbContext, outbox), IPaymentsIntegrationEventPublisher;

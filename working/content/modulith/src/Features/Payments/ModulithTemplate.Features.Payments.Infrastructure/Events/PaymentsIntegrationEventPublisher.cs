using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Payments.Infrastructure.Events;

/// <summary>Binds the Payments publisher marker to the Payments context.</summary>
/// <param name="dbContext">The Payments context the event is staged in.</param>
/// <param name="inbox">The inbox the Consumers' rows are staged in.</param>
/// <param name="outbox">The outbox a forwarded event is staged in.</param>
/// <param name="subscriptions">Who receives each event.</param>
internal sealed class PaymentsIntegrationEventPublisher(
    PaymentsContext dbContext, IInbox inbox, IOutbox outbox, IntegrationEventSubscriptions subscriptions)
    : IntegrationEventPublisher<PaymentsContext>(dbContext, inbox, outbox, subscriptions), IPaymentsIntegrationEventPublisher;

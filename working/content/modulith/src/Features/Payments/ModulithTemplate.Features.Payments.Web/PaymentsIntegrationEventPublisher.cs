using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithTemplate.Features.Payments.Web;

/// <summary>Binds the Payments publisher marker to the Payments context.</summary>
/// <remarks>
/// Lives here rather than beside <c>PaymentsRepository</c> because the marker interface is in
/// <c>Application</c>, which a feature's <c>Infrastructure</c> may not depend on — the one place
/// this mechanism's layering differs from the repository's.
/// </remarks>
/// <param name="dbContext">The Payments context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class PaymentsIntegrationEventPublisher(PaymentsContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<PaymentsContext>(dbContext, outbox), IPaymentsIntegrationEventPublisher;

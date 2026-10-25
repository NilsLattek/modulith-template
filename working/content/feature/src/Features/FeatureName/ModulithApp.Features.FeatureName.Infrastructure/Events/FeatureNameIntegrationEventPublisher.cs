using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Infrastructure.Data;
using ModulithApp.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithApp.Features.FeatureName.Infrastructure.Events;

/// <summary>Binds the FeatureName publisher marker to the FeatureName context.</summary>
/// <param name="dbContext">The FeatureName context the event is staged in.</param>
/// <param name="inbox">The inbox the Consumers' rows are staged in.</param>
/// <param name="outbox">The outbox a forwarded event is staged in.</param>
/// <param name="subscriptions">Who receives each event.</param>
internal sealed class FeatureNameIntegrationEventPublisher(
    FeatureNameContext dbContext, IInbox inbox, IOutbox outbox, IntegrationEventSubscriptions subscriptions)
    : IntegrationEventPublisher<FeatureNameContext>(dbContext, inbox, outbox, subscriptions), IFeatureNameIntegrationEventPublisher;

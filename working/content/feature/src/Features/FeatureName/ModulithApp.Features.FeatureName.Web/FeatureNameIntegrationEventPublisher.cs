using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Infrastructure.Data;
using ModulithApp.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithApp.Features.FeatureName.Web;

/// <summary>Binds the FeatureName publisher marker to the FeatureName context.</summary>
/// <remarks>
/// Lives here rather than beside <c>FeatureNameRepository</c> because the marker interface is in
/// <c>Application</c>, which a feature's <c>Infrastructure</c> may not depend on — the one place
/// this mechanism's layering differs from the repository's.
/// </remarks>
/// <param name="dbContext">The FeatureName context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class FeatureNameIntegrationEventPublisher(FeatureNameContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<FeatureNameContext>(dbContext, outbox), IFeatureNameIntegrationEventPublisher;

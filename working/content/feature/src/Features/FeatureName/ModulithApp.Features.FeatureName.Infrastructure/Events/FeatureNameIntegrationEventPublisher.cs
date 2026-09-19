using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Infrastructure.Data;
using ModulithApp.SharedKernel.Outbox.Events;

using Underground.Outbox;

namespace ModulithApp.Features.FeatureName.Infrastructure.Events;

/// <summary>Binds the FeatureName publisher marker to the FeatureName context.</summary>
/// <remarks>
/// Sits opposite the outbox handlers that read rows back: staging one is infrastructure too. The
/// marker it implements is an <c>Application</c> port, which this layer may name — see ADR 0004.
/// </remarks>
/// <param name="dbContext">The FeatureName context the event is staged in.</param>
/// <param name="outbox">The outbox the row is staged in.</param>
internal sealed class FeatureNameIntegrationEventPublisher(FeatureNameContext dbContext, IOutbox outbox)
    : IntegrationEventPublisher<FeatureNameContext>(dbContext, outbox), IFeatureNameIntegrationEventPublisher;

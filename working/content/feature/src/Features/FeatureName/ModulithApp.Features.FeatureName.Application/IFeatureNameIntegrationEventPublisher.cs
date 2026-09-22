using ModulithApp.SharedKernel.Application.Events;

namespace ModulithApp.Features.FeatureName.Application;

/// <summary>
/// Publishes the FeatureName feature's integration events, through the FeatureName <c>DbContext</c>.
/// </summary>
/// <remarks>
/// The per-feature marker is what binds the registration to this feature's own context, exactly as
/// <see cref="IFeatureNameRepository{T}"/> does; registering the shared
/// <see cref="IIntegrationEventPublisher"/> would let the last feature registered win for every
/// feature, and events would be staged on a sibling's connection.
/// </remarks>
public interface IFeatureNameIntegrationEventPublisher : IIntegrationEventPublisher;

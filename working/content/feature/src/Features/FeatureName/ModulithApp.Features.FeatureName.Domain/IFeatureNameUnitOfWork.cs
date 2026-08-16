using ModulithApp.SharedKernel.Domain;

namespace ModulithApp.Features.FeatureName.Domain;

/// <summary>
/// Transaction coordination for the FeatureName feature. The per-feature interface is what binds the
/// DI registration to this feature's own <c>DbContext</c>; registering the shared
/// <see cref="IUnitOfWork"/> would let the last feature registered win for every feature.
/// </summary>
public interface IFeatureNameUnitOfWork : IUnitOfWork;

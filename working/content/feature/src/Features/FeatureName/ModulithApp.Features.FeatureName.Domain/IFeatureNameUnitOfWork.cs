using ModulithApp.SharedKernel.Domain;

namespace ModulithApp.Features.FeatureName.Domain;

/// <summary>
/// Transaction coordination for the FeatureName feature. Each feature declares its own unit of work
/// interface so that the DI registration binds to that feature's own <c>DbContext</c>; registering
/// the shared <see cref="IUnitOfWork"/> per feature would let the last feature registered win for
/// every feature.
/// </summary>
public interface IFeatureNameUnitOfWork : IUnitOfWork;

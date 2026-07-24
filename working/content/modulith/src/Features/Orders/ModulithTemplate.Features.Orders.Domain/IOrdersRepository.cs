using ModulithTemplate.FeatureCore;

namespace ModulithTemplate.Features.Orders.Domain;

/// <summary>
/// Repository abstraction for the Orders feature. Each feature declares its own repository
/// interface so that the open-generic DI registration binds to that feature's own
/// <c>DbContext</c>; registering the shared <see cref="IRepository{T}"/> per feature would
/// let the last feature registered win for every feature.
/// </summary>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
public interface IOrdersRepository<T> : IRepository<T> where T : class;

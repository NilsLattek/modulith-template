using ModulithTemplate.SharedKernel.Domain;

namespace ModulithTemplate.Features.Orders.Domain;

/// <summary>
/// Repository abstraction for the Orders feature. The per-feature interface is what binds the
/// open-generic DI registration to this feature's own <c>DbContext</c>; registering the shared
/// <see cref="IRepository{T}"/> would let the last feature registered win for every feature.
/// </summary>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
public interface IOrdersRepository<T> : IRepository<T> where T : class;

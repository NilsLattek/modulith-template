using ModulithTemplate.SharedKernel.Domain;

namespace ModulithTemplate.Features.Orders.Domain;

/// <summary>
/// Transaction coordination for the Orders feature. The per-feature interface is what binds the DI
/// registration to this feature's own <c>DbContext</c>; registering the shared
/// <see cref="IUnitOfWork"/> would let the last feature registered win for every feature.
/// </summary>
public interface IOrdersUnitOfWork : IUnitOfWork;

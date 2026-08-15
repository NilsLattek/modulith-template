using ModulithTemplate.SharedKernel.Domain;

namespace ModulithTemplate.Features.Orders.Domain;

/// <summary>
/// Transaction coordination for the Orders feature. Each feature declares its own unit of work
/// interface so that the DI registration binds to that feature's own <c>DbContext</c>; registering
/// the shared <see cref="IUnitOfWork"/> per feature would let the last feature registered win for
/// every feature.
/// </summary>
public interface IOrdersUnitOfWork : IUnitOfWork;

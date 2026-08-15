using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.Features.Orders.Infrastructure.Data;

internal sealed class OrdersUnitOfWork(OrdersContext dbContext)
    : UnitOfWorkBase<OrdersContext>(dbContext), IOrdersUnitOfWork;

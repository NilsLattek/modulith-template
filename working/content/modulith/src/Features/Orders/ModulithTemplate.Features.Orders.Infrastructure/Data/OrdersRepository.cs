using Ardalis.Specification.EntityFrameworkCore;

using ModulithTemplate.Features.Orders.Domain;

namespace ModulithTemplate.Features.Orders.Infrastructure.Data;

internal sealed class OrdersRepository<T>(OrdersContext dbContext)
    : RepositoryBase<T>(dbContext), IOrdersRepository<T> where T : class;

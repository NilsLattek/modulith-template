using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.Features.Orders.Infrastructure.Data;

internal sealed class OrdersRepository<T>(OrdersContext dbContext)
    : FeatureRepository<T>(dbContext), IOrdersRepository<T> where T : class;

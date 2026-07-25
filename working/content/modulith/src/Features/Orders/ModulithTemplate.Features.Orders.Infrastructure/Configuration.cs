using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.Infrastructure.Common;

namespace ModulithTemplate.Features.Orders.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigureOrdersInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrdersContext>(configuration, schema: "orders");
        services.AddScoped(typeof(IOrdersRepository<>), typeof(OrdersRepository<>));
        return services;
    }
}

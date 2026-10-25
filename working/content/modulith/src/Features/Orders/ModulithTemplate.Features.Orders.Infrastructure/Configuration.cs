using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.Features.Orders.Infrastructure.Events;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.Features.Orders.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigureOrdersInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrdersContext>(configuration, schema: "orders");
        services.AddScoped(typeof(IOrdersRepository<>), typeof(OrdersRepository<>));
        services.AddScoped<IOrdersUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped<IOrdersIntegrationEventPublisher, OrdersIntegrationEventPublisher>();

        // To send one of this feature's events outside the process (a broker, email, HTTP), declare an
        // IOutboxMessageHandler<T> for it and call the source-generated
        // services.AddModulithTemplateFeaturesOrdersInfrastructureMessageHandlers() here; the
        // generator emits that method only once this assembly declares a handler.
        return services;
    }
}

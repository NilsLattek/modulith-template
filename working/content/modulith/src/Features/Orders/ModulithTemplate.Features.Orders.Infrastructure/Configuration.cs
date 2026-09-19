using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Infrastructure;

using Underground.Outbox.Configuration;

namespace ModulithTemplate.Features.Orders.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigureOrdersInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrdersContext>(configuration, schema: "orders");
        services.AddScoped(typeof(IOrdersRepository<>), typeof(OrdersRepository<>));
        services.AddScoped<IOrdersUnitOfWork, OrdersUnitOfWork>();

        // Source-generated, one method per assembly and named after it: it registers every
        // IOutboxMessageHandler<T> under OutboxHandlers/, so a second published event costs a
        // handler class and no change here.
        services.AddModulithTemplateFeaturesOrdersInfrastructureMessageHandlers();

        return services;
    }
}

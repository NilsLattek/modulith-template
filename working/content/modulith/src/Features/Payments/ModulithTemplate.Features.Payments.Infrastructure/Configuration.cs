using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.Features.Payments.Infrastructure.Events;
using ModulithTemplate.SharedKernel.Infrastructure;

using Underground.Outbox.Configuration;

namespace ModulithTemplate.Features.Payments.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigurePaymentsInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PaymentsContext>(configuration, schema: "payments");
        services.AddScoped(typeof(IPaymentsRepository<>), typeof(PaymentsRepository<>));
        services.AddScoped<IPaymentsUnitOfWork, PaymentsUnitOfWork>();
        services.AddScoped<IPaymentsIntegrationEventPublisher, PaymentsIntegrationEventPublisher>();

        // Source-generated, one method per assembly and named after it: it registers every inbox handler
        // under InboxHandlers/, so consuming another event costs a handler class and no change here.
        // It exists only while this assembly declares a handler.
        services.AddModulithTemplateFeaturesPaymentsInfrastructureMessageHandlers();
        return services;
    }
}

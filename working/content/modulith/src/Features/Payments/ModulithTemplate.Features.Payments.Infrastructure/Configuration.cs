using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.Features.Payments.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigurePaymentsInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PaymentsContext>(configuration, schema: "payments");
        services.AddScoped(typeof(IPaymentsRepository<>), typeof(PaymentsRepository<>));
        services.AddScoped<IPaymentsUnitOfWork, PaymentsUnitOfWork>();

        // This feature publishes no Integration Event yet. The first one is a record in Contracts, a
        // SomethingHappenedOutboxHandler : IOutboxMessageHandler<T> under OutboxHandlers/, and then
        // services.AddModulithTemplateFeaturesPaymentsInfrastructureMessageHandlers() here — the
        // source generator emits that method only once this assembly declares a handler.
        return services;
    }
}

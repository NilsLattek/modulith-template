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
        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithApp.Features.FeatureName.Domain;
using ModulithApp.Features.FeatureName.Infrastructure.Data;
using ModulithApp.SharedKernel.Infrastructure;

namespace ModulithApp.Features.FeatureName.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigureFeatureNameInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<FeatureNameContext>(configuration, schema: "featureschema");
        services.AddScoped(typeof(IFeatureNameRepository<>), typeof(FeatureNameRepository<>));
        services.AddScoped<IFeatureNameUnitOfWork, FeatureNameUnitOfWork>();
        return services;
    }
}

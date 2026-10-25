using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Domain;
using ModulithApp.Features.FeatureName.Infrastructure.Data;
using ModulithApp.Features.FeatureName.Infrastructure.Events;
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
        services.AddScoped<IFeatureNameIntegrationEventPublisher, FeatureNameIntegrationEventPublisher>();

        // To consume another feature's Integration Event, declare a SomethingHappenedInboxHandler :
        // IntegrationEventInboxTransport<T, FeatureNameContext> under InboxHandlers/, mapping it to one
        // of this feature's Commands. To send one of this feature's own events outside the process,
        // declare an IOutboxMessageHandler<T>. Then call
        // services.AddModulithAppFeaturesFeatureNameInfrastructureMessageHandlers() here — the source
        // generator emits that method only once this assembly declares a handler.
        return services;
    }
}

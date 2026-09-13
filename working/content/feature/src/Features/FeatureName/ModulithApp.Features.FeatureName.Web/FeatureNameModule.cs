using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Infrastructure;

namespace ModulithApp.Features.FeatureName.Web;

public static class FeatureNameModule
{
    public static WebApplicationBuilder ConfigureFeatureNameFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureFeatureNameInfrastructure(builder.Configuration);
        builder.Services.ConfigureFeatureNameApplication();

        // Bound here, not in Configure*Infrastructure: the marker interface lives in Application and
        // Infrastructure may not reference it.
        builder.Services.AddScoped<IFeatureNameIntegrationEventPublisher, FeatureNameIntegrationEventPublisher>();
        return builder;
    }
}

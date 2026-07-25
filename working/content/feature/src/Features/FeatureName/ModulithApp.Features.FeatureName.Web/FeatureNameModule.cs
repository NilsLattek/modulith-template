using Microsoft.AspNetCore.Builder;

using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Infrastructure;

namespace ModulithApp.Features.FeatureName.Web;

public static class FeatureNameModule
{
    public static WebApplicationBuilder ConfigureFeatureNameFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureFeatureNameInfrastructure(builder.Configuration);
        builder.Services.ConfigureFeatureNameApplication();
        return builder;
    }
}

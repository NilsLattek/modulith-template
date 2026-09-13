using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Infrastructure;

namespace ModulithTemplate.Features.Payments.Web;

public static class PaymentsModule
{
    public static WebApplicationBuilder ConfigurePaymentsFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigurePaymentsInfrastructure(builder.Configuration);
        builder.Services.ConfigurePaymentsApplication();

        // Bound here, not in Configure*Infrastructure: the marker interface lives in Application and
        // Infrastructure may not reference it.
        builder.Services.AddScoped<IPaymentsIntegrationEventPublisher, PaymentsIntegrationEventPublisher>();
        return builder;
    }
}

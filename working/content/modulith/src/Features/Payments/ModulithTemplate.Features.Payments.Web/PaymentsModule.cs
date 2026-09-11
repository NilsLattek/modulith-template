using Microsoft.AspNetCore.Builder;

using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Infrastructure;

namespace ModulithTemplate.Features.Payments.Web;

public static class PaymentsModule
{
    public static WebApplicationBuilder ConfigurePaymentsFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigurePaymentsInfrastructure(builder.Configuration);
        builder.Services.ConfigurePaymentsApplication();
        return builder;
    }
}

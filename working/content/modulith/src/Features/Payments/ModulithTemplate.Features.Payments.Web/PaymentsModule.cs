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

        // Here, not only in the host: the validation source generator emits metadata for the
        // [ValidatableType] form models of the assembly that calls AddValidation.
        builder.Services.AddValidation();

        return builder;
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Infrastructure;

namespace ModulithTemplate.Features.Orders.Web;

public static class OrdersModule
{
    public static WebApplicationBuilder ConfigureOrdersFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureOrdersInfrastructure(builder.Configuration);
        builder.Services.ConfigureOrdersApplication();

        // Here, not only in the host: the validation source generator emits metadata for the
        // [ValidatableType] form models of the assembly that calls AddValidation.
        builder.Services.AddValidation();

        return builder;
    }
}

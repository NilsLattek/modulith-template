using Microsoft.AspNetCore.Builder;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Infrastructure;

namespace ModulithTemplate.Features.Orders.Web;

public static class OrdersModule
{
    public static WebApplicationBuilder ConfigureOrdersFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureOrdersInfrastructure(builder.Configuration);
        builder.Services.ConfigureOrdersApplication();
        return builder;
    }
}

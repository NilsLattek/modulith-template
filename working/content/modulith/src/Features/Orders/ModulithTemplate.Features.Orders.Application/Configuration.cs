using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Api;
using ModulithTemplate.Features.Orders.Contracts.Api;

namespace ModulithTemplate.Features.Orders.Application;

public static class Configuration
{
    // The assembly scan registers every AbstractValidator<T> as IValidator<T>, which is what the
    // host's ValidationBehaviour resolves — a new *CommandValidator is picked up without touching
    // this file. The module API is published from Contracts but registered here, by its owner.
    public static IServiceCollection ConfigureOrdersApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrdersApi, OrdersApi>();
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

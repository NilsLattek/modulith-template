using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Api;
using ModulithTemplate.Features.Orders.Contracts.Api;

namespace ModulithTemplate.Features.Orders.Application;

public static class Configuration
{
    // Scans this assembly for AbstractValidator<T> implementations and registers each as
    // IValidator<T>, which is what the host's ValidationBehaviour resolves. A new *CommandValidator
    // beside its command is therefore picked up without touching this file.
    //
    // The module API is registered here too: the contract is published from Contracts, but the
    // implementation is this feature's to own and wire up. Other features resolve IOrdersApi.
    public static IServiceCollection ConfigureOrdersApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrdersApi, OrdersApi>();
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

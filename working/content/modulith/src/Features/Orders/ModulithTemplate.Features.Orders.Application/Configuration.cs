using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Api;
using ModulithTemplate.Features.Orders.Contracts.Api;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.Features.Orders.Application;

public static class Configuration
{
    // The assembly scan registers every AbstractValidator<T> as IValidator<T>, which is what the
    // host's ValidationBehaviour resolves — a new *CommandValidator is picked up without touching
    // this file. The module API is published from Contracts but registered here, by its owner.
    //
    // Integration events are declared here for the same reason: Application is the only layer
    // allowed to reference a Contracts project, and the worker needs the type to turn a stored row
    // back into the event.
    public static IServiceCollection ConfigureOrdersApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrdersApi, OrdersApi>();
        services.AddIntegrationEvent<SomeEntityAddedIntegrationEvent>();
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

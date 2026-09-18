using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Api;
using ModulithTemplate.Features.Orders.Contracts.Api;

using Underground.Outbox.Configuration;

namespace ModulithTemplate.Features.Orders.Application;

public static class Configuration
{
    // The assembly scan registers every AbstractValidator<T> as IValidator<T>, which is what the
    // host's ValidationBehaviour resolves — a new *CommandValidator is picked up without touching
    // this file. The module API is published from Contracts but registered here, by its owner.
    //
    // Outbox handlers are registered here for the same reason: Application is the only layer allowed
    // to reference a Contracts project, so it is the only one that can name an integration event.
    public static IServiceCollection ConfigureOrdersApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrdersApi, OrdersApi>();

        // Source-generated, one method per assembly and named after it: it registers every
        // IOutboxMessageHandler<T> under OutboxHandlers/, so a second published event costs a
        // handler class and no change here.
        services.AddModulithTemplateFeaturesOrdersApplicationMessageHandlers();

        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

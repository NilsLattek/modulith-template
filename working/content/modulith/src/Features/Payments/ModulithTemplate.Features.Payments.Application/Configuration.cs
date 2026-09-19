using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.Features.Payments.Application;

public static class Configuration
{
    // The assembly scan registers every AbstractValidator<T> as IValidator<T>, which is what the
    // host's ValidationBehaviour resolves — a new *CommandValidator is picked up without touching
    // this file.
    public static IServiceCollection ConfigurePaymentsApplication(this IServiceCollection services)
    {
        // Consuming a sibling's Integration Event costs nothing here: an INotificationHandler<T>
        // under IntegrationEventHandlers/ is registered by the mediator, as SomeEntityAdded's is.
        // Publishing one is Infrastructure's side — see ConfigurePaymentsInfrastructure.
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

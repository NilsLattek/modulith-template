using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace ModulithApp.Features.FeatureName.Application;

public static class Configuration
{
    // The assembly scan registers every AbstractValidator<T> as IValidator<T>, which is what the
    // host's ValidationBehaviour resolves — a new *CommandValidator is picked up without touching
    // this file.
    public static IServiceCollection ConfigureFeatureNameApplication(this IServiceCollection services)
    {
        // Consuming a sibling's Integration Event costs nothing here: an INotificationHandler<T>
        // under IntegrationEventHandlers/ is registered by the mediator. Publishing one is
        // Infrastructure's side — see ConfigureFeatureNameInfrastructure.
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

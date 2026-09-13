using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using ModulithApp.SharedKernel.Application.Events;

namespace ModulithApp.Features.FeatureName.Application;

public static class Configuration
{
    // The assembly scan registers every AbstractValidator<T> as IValidator<T>, which is what the
    // host's ValidationBehaviour resolves — a new *CommandValidator is picked up without touching
    // this file.
    public static IServiceCollection ConfigureFeatureNameApplication(this IServiceCollection services)
    {
        // Every integration event this feature publishes is declared here, so the worker can turn a
        // stored row back into it: services.AddIntegrationEvent<SomethingHappenedIntegrationEvent>().
        // Here because Application is the only layer allowed to reference a Contracts project; the
        // using above is what that call needs.
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

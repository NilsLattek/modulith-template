using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.Features.Payments.Application;

public static class Configuration
{
    // The assembly scan registers every AbstractValidator<T> as IValidator<T>, which is what the
    // host's ValidationBehaviour resolves — a new *CommandValidator is picked up without touching
    // this file.
    public static IServiceCollection ConfigurePaymentsApplication(this IServiceCollection services)
    {
        // Every integration event this feature publishes is declared here, so the worker can turn a
        // stored row back into it: services.AddIntegrationEvent<SomethingHappenedIntegrationEvent>().
        // Here because Application is the only layer allowed to reference a Contracts project; the
        // using above is what that call needs.
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

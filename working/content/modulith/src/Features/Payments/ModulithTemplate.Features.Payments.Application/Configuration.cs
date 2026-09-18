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
        // This feature publishes no Integration Event yet. The first one is a record in Contracts,
        // a SomethingHappenedOutboxHandler : IOutboxMessageHandler<T> under OutboxHandlers/, and
        // then services.AddModulithTemplateFeaturesPaymentsApplicationMessageHandlers() here — the
        // source generator emits that method only once this assembly declares a handler. Here
        // because Application is the only layer allowed to reference a Contracts project.
        //
        // Consuming a sibling's event needs none of this: an INotificationHandler<T> under
        // IntegrationEventHandlers/ is registered by the mediator, as SomeEntityAdded's is.
        return services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
    }
}

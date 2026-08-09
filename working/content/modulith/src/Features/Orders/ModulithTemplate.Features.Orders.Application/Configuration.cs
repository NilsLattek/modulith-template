using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.Features.Orders.Application;

public static class Configuration
{
    // Scans this assembly for AbstractValidator<T> implementations and registers each as
    // IValidator<T>, which is what the host's ValidationBehaviour resolves. A new *CommandValidator
    // beside its command is therefore picked up without touching this file.
    public static IServiceCollection ConfigureOrdersApplication(this IServiceCollection services) =>
        services.AddValidatorsFromAssembly(typeof(Configuration).Assembly);
}

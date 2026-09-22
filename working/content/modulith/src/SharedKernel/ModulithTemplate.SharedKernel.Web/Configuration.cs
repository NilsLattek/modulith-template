using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.SharedKernel.Web;

public static class Configuration
{
    /// <summary>Registers the scope-per-message mediator components send database work through.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddScopedMediator(this IServiceCollection services) =>
        services.AddSingleton<IScopedMediator, ScopedMediator>();
}

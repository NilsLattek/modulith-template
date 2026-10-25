using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.WebTests;

/// <summary>
/// Every domain event handler a feature declares is registered by the host's generated mediator.
/// </summary>
/// <remarks>
/// The generator registers only handlers whose event type it can see in the host's compilation;
/// one it cannot see is skipped at build time and its event dropped at run time, with no error.
/// </remarks>
public class DomainEventHandlerRegistrationTests
{
    [Fact]
    public void Host_mediator_registers_every_feature_domain_event_handler()
    {
        var handlers = FeatureAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.GetInterfaces().Any(IsDomainEventHandler))
            .ToList();
        Assert.NotEmpty(handlers);

        // The host's generated AddMediator: this project references the host, not the generator.
        // The lifetime must match Program.cs, or it throws.
        var services = new ServiceCollection();
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        Assert.All(handlers, handler => Assert.True(
            services.Any(descriptor => descriptor.ServiceType == handler),
            $"{handler.FullName} is not registered by the host's mediator; its domain event would be dropped."));
    }

    private static bool IsDomainEventHandler(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>);

    private static IEnumerable<Assembly> FeatureAssemblies() =>
        Directory.GetFiles(AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileNameWithoutExtension(path).Contains(".Features.", StringComparison.Ordinal))
            // Load by name, into the default context, so the types match those the mediator registered.
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)));
}

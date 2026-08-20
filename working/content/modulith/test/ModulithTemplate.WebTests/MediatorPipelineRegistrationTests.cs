using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Behaviours;

namespace ModulithTemplate.WebTests;

/// <summary>Guards that behaviours stay reachable from <c>AddMediator</c> once they live in another assembly.</summary>
public class MediatorPipelineRegistrationTests
{
    [Fact]
    public void AddMediator_registers_the_solution_pipeline_behaviours_in_order()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehaviour<,>),
                typeof(ExceptionBehaviour<,>),
                typeof(ValidationBehaviour<,>),
            ];
        });

        // Assert — Mediator's source generator closes each configured behaviour over every message
        // type it discovers in the compilation rather than registering the open-generic type itself,
        // so compare the distinct implementation generic type definitions, in first-seen order.
        var registeredBehaviourDefinitions = services
            .Where(descriptor => descriptor.ServiceType.IsGenericType
                && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
            .Select(descriptor => descriptor.ImplementationType!.GetGenericTypeDefinition())
            .Distinct()
            .ToArray();

        Assert.Equal(
            [typeof(LoggingBehaviour<,>), typeof(ExceptionBehaviour<,>), typeof(ValidationBehaviour<,>)],
            registeredBehaviourDefinitions);
    }
}

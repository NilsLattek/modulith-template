using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Smoke tests for the Orders application layer.</summary>
public class OrdersApplicationSmokeTests
{
    /// <summary>
    /// Smoke test for the layer's test toolchain: a substituted <see cref="IOrdersRepository{T}"/>
    /// is accepted by the container and <c>ConfigureOrdersApplication</c> composes onto it.
    /// Handler behaviour is covered by the per-handler tests alongside this file; this one only
    /// guards the layer's DI composition.
    /// </summary>
    [Fact]
    public void ConfigureOrdersApplication_with_a_substituted_repository_builds_a_resolvable_provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IOrdersRepository<SomeEntity>>());

        // Act
        using var provider = services.ConfigureOrdersApplication().BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IOrdersRepository<SomeEntity>>());
    }

    /// <summary>
    /// Guards the assembly scan in <c>ConfigureOrdersApplication</c>: the host's validation
    /// behaviour resolves <see cref="IValidator{T}"/>, so a validator that is written but never
    /// registered would silently never run.
    /// </summary>
    [Fact]
    public void ConfigureOrdersApplication_registers_the_feature_validators()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        using var provider = services.ConfigureOrdersApplication().BuildServiceProvider();

        // Assert
        Assert.IsType<AddSomeEntityCommandValidator>(provider.GetRequiredService<IValidator<AddSomeEntityCommand>>());
    }
}

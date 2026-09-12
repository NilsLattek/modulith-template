using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.Features.Payments.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Payments.ApplicationTests;

/// <summary>Smoke tests for the Payments application layer.</summary>
public class PaymentsApplicationSmokeTests
{
    /// <summary>
    /// Guards the layer's DI composition only: a substituted <see cref="IPaymentsRepository{T}"/>
    /// is accepted by the container and <c>ConfigurePaymentsApplication</c> composes onto it.
    /// </summary>
    [Fact]
    public void ConfigurePaymentsApplication_with_a_substituted_repository_builds_a_resolvable_provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IPaymentsRepository<Payment>>());

        // Act
        using var provider = services.ConfigurePaymentsApplication().BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IPaymentsRepository<Payment>>());
    }
}

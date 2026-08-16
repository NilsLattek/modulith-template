using Microsoft.Extensions.DependencyInjection;

using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Domain;

using NSubstitute;

namespace ModulithApp.Features.FeatureName.ApplicationTests;

/// <summary>Smoke tests for the FeatureName application layer.</summary>
public class FeatureNameApplicationSmokeTests
{
    /// <summary>
    /// Stand-in entity; replace with a real one. Not <c>private</c>, because NSubstitute's Castle
    /// proxy generator needs access to it as a generic argument.
    /// </summary>
#pragma warning disable S2094 // Classes should not be empty
    public sealed class TestEntity;
#pragma warning restore S2094 // Classes should not be empty

    /// <summary>
    /// Guards the layer's DI composition only: a substituted <see cref="IFeatureNameRepository{T}"/>
    /// is accepted by the container and <c>ConfigureFeatureNameApplication</c> composes onto it.
    /// </summary>
    [Fact]
    public void ConfigureFeatureNameApplication_with_a_substituted_repository_builds_a_resolvable_provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IFeatureNameRepository<TestEntity>>());

        // Act
        using var provider = services.ConfigureFeatureNameApplication().BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IFeatureNameRepository<TestEntity>>());
    }
}

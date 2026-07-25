using Microsoft.Extensions.DependencyInjection;

using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Domain;

using NSubstitute;

namespace ModulithApp.Features.FeatureName.ApplicationTests;

/// <summary>Smoke tests for the FeatureName application layer.</summary>
public class FeatureNameApplicationSmokeTests
{
    /// <summary>
    /// Stand-in entity; replace with a real domain entity once the feature has one. Must be
    /// visible outside this assembly (not <c>private</c>) because NSubstitute's Castle proxy
    /// generator needs access to it as a generic argument when substituting
    /// <see cref="IFeatureNameRepository{T}"/> below.
    /// </summary>
#pragma warning disable S2094 // Classes should not be empty
    public sealed class TestEntity;
#pragma warning restore S2094 // Classes should not be empty

    /// <summary>
    /// Smoke test for the layer's test toolchain: a substituted <see cref="IFeatureNameRepository{T}"/>
    /// is accepted by the container and <c>ConfigureFeatureNameApplication</c> composes onto it.
    /// Replace this with a real app-service test once the layer registers one.
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

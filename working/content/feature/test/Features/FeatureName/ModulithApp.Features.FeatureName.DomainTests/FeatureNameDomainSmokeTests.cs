using ModulithApp.SharedKernel.Domain;
using ModulithApp.Features.FeatureName.Domain;

namespace ModulithApp.Features.FeatureName.DomainTests;

/// <summary>Smoke tests for the FeatureName domain layer.</summary>
public class FeatureNameDomainSmokeTests
{
    /// <summary>Stand-in entity; replace with a real domain entity once the feature has one.</summary>
#pragma warning disable S2094 // Classes should not be empty
    private sealed class TestEntity;
#pragma warning restore S2094 // Classes should not be empty

    /// <summary>
    /// The feature-owned repository abstraction must extend the shared <see cref="IRepository{T}"/>.
    /// That relationship is what lets the feature's open-generic DI registration bind to its own
    /// <c>DbContext</c> instead of whichever feature registered last.
    /// </summary>
    [Fact]
    public void IFeatureNameRepository_for_a_domain_entity_extends_the_shared_IRepository()
    {
        // Arrange
        var featureRepository = typeof(IFeatureNameRepository<TestEntity>);

        // Act
        var extendsSharedRepository = typeof(IRepository<TestEntity>).IsAssignableFrom(featureRepository);

        // Assert
        Assert.True(extendsSharedRepository);
    }
}

using ModulithTemplate.FeatureCore;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.DomainTests;

/// <summary>Smoke tests for the Orders domain layer.</summary>
public class OrdersDomainSmokeTests
{
    /// <summary>
    /// The feature-owned repository abstraction must extend the shared <see cref="IRepository{T}"/>.
    /// That relationship is what lets the feature's open-generic DI registration bind to its own
    /// <c>DbContext</c> instead of whichever feature registered last.
    /// </summary>
    [Fact]
    public void IOrdersRepository_for_a_domain_entity_extends_the_shared_IRepository()
    {
        // Arrange
        var featureRepository = typeof(IOrdersRepository<SomeEntity>);

        // Act
        var extendsSharedRepository = typeof(IRepository<SomeEntity>).IsAssignableFrom(featureRepository);

        // Assert
        Assert.True(extendsSharedRepository);
    }
}

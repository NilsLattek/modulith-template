using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.Features.Payments.Domain.Entities;
using ModulithTemplate.SharedKernel.Domain;

namespace ModulithTemplate.Features.Payments.DomainTests;

/// <summary>Smoke tests for the Payments domain layer.</summary>
public class PaymentsDomainSmokeTests
{
    /// <summary>
    /// The feature-owned repository abstraction must extend the shared <see cref="IRepository{T}"/>.
    /// That relationship is what lets the feature's open-generic DI registration bind to its own
    /// <c>DbContext</c> instead of whichever feature registered last.
    /// </summary>
    [Fact]
    public void IPaymentsRepository_for_a_domain_entity_extends_the_shared_IRepository()
    {
        // Arrange
        var featureRepository = typeof(IPaymentsRepository<Payment>);

        // Act
        var extendsSharedRepository = typeof(IRepository<Payment>).IsAssignableFrom(featureRepository);

        // Assert
        Assert.True(extendsSharedRepository);
    }
}

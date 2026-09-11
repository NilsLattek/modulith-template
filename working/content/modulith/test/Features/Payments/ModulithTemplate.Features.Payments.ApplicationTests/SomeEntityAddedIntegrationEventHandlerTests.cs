using Ardalis.Specification;

using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Payments.Application.IntegrationEventHandlers;
using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.Features.Payments.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Payments.ApplicationTests;

/// <summary>Tests for <see cref="SomeEntityAddedIntegrationEventHandler"/>.</summary>
public class SomeEntityAddedIntegrationEventHandlerTests
{
    private static SomeEntityAddedIntegrationEvent AnEvent(Guid someEntityId, decimal amount = 12.34m) =>
        new(Guid.CreateVersion7(), someEntityId, "a name", amount);

    [Fact]
    public async Task Handle_records_a_payment_for_the_order_the_event_names()
    {
        // Arrange
        var repository = Substitute.For<IPaymentsRepository<Payment>>();
        var recorded = new List<Payment>();
        await repository.AddAsync(Arg.Do<Payment>(recorded.Add), Arg.Any<CancellationToken>());
        var someEntityId = Guid.CreateVersion7();

        // Act
        await new SomeEntityAddedIntegrationEventHandler(repository)
            .Handle(AnEvent(someEntityId, 12.34m), TestContext.Current.CancellationToken);

        // Assert
        var payment = Assert.Single(recorded);
        Assert.Equal(someEntityId, payment.OrderId);
        Assert.Equal(12.34m, payment.Amount);
    }

    [Fact]
    public async Task Handle_records_nothing_when_the_order_already_has_a_payment()
    {
        // Arrange
        var repository = Substitute.For<IPaymentsRepository<Payment>>();
        repository.AnyAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        // Delivery is at least once — a sibling consumer's failure re-runs this one — so a redelivery
        // must not record the payment twice.
        await new SomeEntityAddedIntegrationEventHandler(repository)
            .Handle(AnEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        await repository.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }
}

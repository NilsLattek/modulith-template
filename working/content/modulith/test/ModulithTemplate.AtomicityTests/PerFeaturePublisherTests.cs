using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.Features.Payments.Application;
using ModulithTemplate.Features.Payments.Infrastructure.Data;

using Underground.Outbox.Data;

namespace ModulithTemplate.AtomicityTests;

/// <summary>
/// What a feature's own publisher marker buys: the row is staged on that feature's connection
/// rather than on whichever feature was registered last.
/// </summary>
/// <remarks>
/// Asserted through Payments, which publishes nothing of its own, because that is the shape the
/// <c>modulith-feature</c> sub-template scaffolds — a feature wired to publish before it has an
/// event to publish. Bound to a sibling's context this still compiles and still stages a row, and
/// the mistake surfaces only as an event lost with a save it never belonged to.
/// </remarks>
public class PerFeaturePublisherTests
{
    [Fact]
    public async Task A_features_publisher_stages_through_that_features_context()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        using var scope = solution.CreateScope();

        // Act
        // Orders' event for want of one of its own; what is under test is which context receives the
        // row, not what the row says.
        scope.ServiceProvider.GetRequiredService<IPaymentsIntegrationEventPublisher>()
            .Publish(new SomeEntityAddedIntegrationEvent(
                Guid.CreateVersion7(), Guid.CreateVersion7(), "a name", 12.34m));

        // Assert
        Assert.Single(scope.ServiceProvider.GetRequiredService<PaymentsContext>()
            .ChangeTracker.Entries<OutboxMessage>());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<OrdersContext>()
            .ChangeTracker.Entries<OutboxMessage>());
    }
}

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Infrastructure.OutboxHandlers;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// The other half of the crossing: a claimed row becomes its event again and reaches the consuming
/// feature, which commits through its own context.
/// </summary>
/// <remarks>
/// Exercised through the handler registry the worker looks rows up in rather than the handler directly,
/// because what is under test is the wiring — that the feature owning the event contributed a handler
/// for it, and that the row finds it.
/// </remarks>
public class IntegrationEventDeliveryTests
{
    private static Task DispatchAsync(IServiceScope scope, OutboxMessage row, CancellationToken cancellationToken)
    {
        var registry = scope.ServiceProvider.GetRequiredService<HandlerRegistry<OutboxMessage>>();
        Assert.True(registry.TryGetEntry(row.Type, out var entry));
        return entry.HandleAsync(
            scope.ServiceProvider, row, new MessageMetadata(row.EventId, row.GroupKey), cancellationToken);
    }

    [Fact]
    public async Task The_publishing_feature_claims_its_own_event()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();

        // Act
        using var scope = solution.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<HandlerRegistry<OutboxMessage>>();

        // Assert
        // ConfigureOrdersApplication calls the registration method the outbox generator emitted for
        // that assembly; without it the row below would fail as an unclaimed type.
        Assert.True(registry.TryGetEntry(typeof(SomeEntityAddedIntegrationEvent).FullName!, out var entry));
        Assert.Equal(typeof(SomeEntityAddedOutboxHandler), entry.HandlerType);
    }

    [Fact]
    public async Task A_staged_row_reaches_the_consuming_feature()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        await solution.SendAsync(new AddSomeEntityCommand("a name", 12.34m), cancellationToken);
        var staged = Assert.Single(await solution.OutboxRowsAsync(cancellationToken));

        // Act
        // A scope of its own, as the worker gives each message: the consumer writes through its own
        // context, never the publisher's.
        using var scope = solution.CreateScope();
        await DispatchAsync(scope, staged, cancellationToken);

        // Assert
        // Orders' handler published to the mediator, and Payments' consumer is what reacted — one
        // outbox handler, fan-out by notification.
        var payment = Assert.Single(await solution.PaymentsAsync(cancellationToken));
        Assert.Equal(staged.GroupKey, payment.OrderId.ToString(), StringComparer.Ordinal);
        Assert.Equal(12.34m, payment.Amount);
    }
}

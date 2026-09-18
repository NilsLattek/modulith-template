using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Application.OutboxHandlers;
using ModulithTemplate.Features.Orders.Contracts.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;
using Underground.Outbox.Exceptions;

namespace ModulithTemplate.AtomicityTests;

/// <summary>
/// The other half of the crossing: a claimed row becomes its event again and reaches the consuming
/// feature, which commits through its own context.
/// </summary>
/// <remarks>
/// Exercised through the dispatcher the worker resolves rather than the handler directly, because
/// what is under test is the wiring — that the feature owning the event contributed a handler for it,
/// and that the row finds it.
/// </remarks>
public class IntegrationEventDeliveryTests
{
    private static IMessageDispatcher<OutboxMessage> Dispatcher(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IMessageDispatcher<OutboxMessage>>();

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
        await Dispatcher(scope).ExecuteAsync(scope, staged, cancellationToken);

        // Assert
        // Orders' handler published to the mediator, and Payments' consumer is what reacted — one
        // outbox handler, fan-out by notification.
        var payment = Assert.Single(await solution.PaymentsAsync(cancellationToken));
        Assert.Equal(staged.GroupKey, payment.OrderId.ToString(), StringComparer.Ordinal);
        Assert.Equal(12.34m, payment.Amount);
    }

    [Fact]
    public async Task An_unclaimed_event_type_fails_loudly()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var unknown = new OutboxMessage(
            Guid.CreateVersion7(), DateTime.UtcNow, "Nothing.Handles.This", "{}", "a-group", null);

        // Act
        using var scope = solution.CreateScope();
        var dispatching = async () => await Dispatcher(scope).ExecuteAsync(scope, unknown, cancellationToken);

        // Assert
        // An event no feature claims is a wiring mistake, and the row must not be silently completed.
        var exception = await Assert.ThrowsAsync<ParsingException>(dispatching);
        Assert.Contains("Nothing.Handles.This", exception.Message, StringComparison.Ordinal);
    }
}

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;
using Underground.Outbox.Exceptions;

namespace ModulithTemplate.AtomicityTests;

/// <summary>
/// The other half of the crossing: a claimed row becomes its event again and reaches the consuming
/// feature, which commits through its own context.
/// </summary>
/// <remarks>
/// Exercised through the dispatcher the worker resolves, not the republisher directly, because the
/// registration order in <c>Program.cs</c> is what decides which dispatcher that is.
/// </remarks>
public class IntegrationEventRepublisherTests
{
    private static IMessageDispatcher<OutboxMessage> Dispatcher(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IMessageDispatcher<OutboxMessage>>();

    [Fact]
    public async Task The_worker_dispatches_through_the_republisher()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();

        // Act
        using var scope = solution.CreateScope();

        // Assert
        // AddIntegrationEventDelivery only wins because it follows AddOutboxServices; the other way
        // round the worker would get the generated dispatcher, which throws for every row.
        Assert.IsType<IntegrationEventRepublisher>(Dispatcher(scope));
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
        var payment = Assert.Single(await solution.PaymentsAsync(cancellationToken));
        Assert.Equal(staged.GroupKey, payment.OrderId.ToString(), StringComparer.Ordinal);
        Assert.Equal(12.34m, payment.Amount);
    }

    [Fact]
    public async Task An_unregistered_event_type_fails_loudly()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var unknown = new OutboxMessage(
            Guid.CreateVersion7(), DateTime.UtcNow, "Nothing.Registers.This", "{}", "a-group", null);

        // Act
        using var scope = solution.CreateScope();
        var republishing = async () => await Dispatcher(scope).ExecuteAsync(scope, unknown, cancellationToken);

        // Assert
        // An event nobody registered is a wiring mistake, and the row must not be silently completed.
        var exception = await Assert.ThrowsAsync<ParsingException>(republishing);
        Assert.Contains("Nothing.Registers.This", exception.Message, StringComparison.Ordinal);
    }
}

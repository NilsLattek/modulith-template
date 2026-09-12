using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.SharedKernel.Application.Events;
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

    /// <summary>Stages a row, reports it as having failed that often, and republishes it.</summary>
    /// <remarks>
    /// Constructed rather than resolved, unlike the tests above: the composed solution clears its
    /// logging providers, so a resolved republisher writes where no test can read.
    /// </remarks>
    /// <param name="retryCount">Failed attempts the worker has already recorded for the row.</param>
    /// <param name="cancellationToken">Cancels the republish.</param>
    /// <returns>The row as it was dispatched, and what was logged while dispatching it.</returns>
    private static async Task<(OutboxMessage Message, FakeLogCollector Log)> RepublishAfterFailuresAsync(
        int retryCount, CancellationToken cancellationToken)
    {
        await using var solution = SolutionUnderTest.Start();
        await solution.SendAsync(new AddSomeEntityCommand("a name", 12.34m), cancellationToken);

        // Read untracked, so this stands in for the worker's claim without writing the count back.
        var staged = Assert.Single(await solution.OutboxRowsAsync(cancellationToken));
        staged.RetryCount = retryCount;

        var logger = new FakeLogger<IntegrationEventRepublisher>();
        using var scope = solution.CreateScope();
        var republisher = new IntegrationEventRepublisher(
            scope.ServiceProvider.GetRequiredService<IntegrationEventRegistry>(), logger);
        await republisher.ExecuteAsync(scope, staged, cancellationToken);

        return (staged, logger.Collector);
    }

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

    [Fact]
    public async Task A_message_at_the_failure_threshold_warns_with_the_event_and_its_identity()
    {
        // Arrange & Act
        var (message, log) = await RepublishAfterFailuresAsync(
            IntegrationEventRepublisher.RepeatedFailureThreshold, TestContext.Current.CancellationToken);

        // Assert
        // A stuck message blocks its Group forever and nothing else reports that, so the warning has
        // to name what is stuck and which one it is.
        var warning = Assert.Single(log.GetSnapshot(), record => record.Level == LogLevel.Warning);
        Assert.Contains(message.Type, warning.Message, StringComparison.Ordinal);
        Assert.Contains(message.EventId.ToString(), warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_message_below_the_failure_threshold_warns_about_nothing()
    {
        // Arrange & Act
        var (_, log) = await RepublishAfterFailuresAsync(
            IntegrationEventRepublisher.RepeatedFailureThreshold - 1, TestContext.Current.CancellationToken);

        // Assert
        // Backoff absorbs a transient outage; warning about one would train the operator to ignore
        // the warning that matters.
        Assert.DoesNotContain(log.GetSnapshot(), record => record.Level == LogLevel.Warning);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Infrastructure.OutboxHandlers;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// The middle hop: a claimed outbox row is published in-process, and every Consumer adds its inbox row
/// once, however often the outbox delivers it.
/// </summary>
/// <remarks>
/// Dispatched through the handler registry the outbox worker looks rows up in, so what is under test is the
/// wiring as well as the handler: that the publishing feature contributed one for its event.
/// </remarks>
public class IntegrationEventFanOutTests
{
    [Fact]
    public async Task The_publishing_feature_handles_its_own_events_outbox_row()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();

        // Act
        using var scope = solution.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<HandlerRegistry<OutboxMessage>>();

        // Assert
        Assert.True(registry.TryGetEntry(typeof(SomeEntityAddedIntegrationEvent).FullName!, out var entry));
        Assert.Equal(typeof(SomeEntityAddedOutboxHandler), entry.HandlerType);
    }

    [Fact]
    public async Task Each_consumer_gets_one_row_grouped_by_itself_and_the_aggregate()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var outboxRow = await PublishAsync(solution, cancellationToken);

        // Act
        await DispatchAsync(solution, outboxRow, cancellationToken);

        // Assert
        // Payments and Auditing consume it; Orders, which handles nothing, gets no row of its own.
        var rows = await solution.ReadInboxRowsAsync(cancellationToken);
        Assert.Equal(
            [$"AuditContext:{outboxRow.GroupKey}", $"PaymentsContext:{outboxRow.GroupKey}"],
            rows.Select(row => row.GroupKey).Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(
            rows,
            row => string.Equals(
                row.Type,
                typeof(InboxEnvelope<PaymentsContext, SomeEntityAddedIntegrationEvent>).FullName,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_redelivered_outbox_row_succeeds_without_adding_rows()
    {
        // Arrange
        // What a lost completion looks like: the rows were saved, then the outbox offers the row again.
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var outboxRow = await PublishAsync(solution, cancellationToken);
        await DispatchAsync(solution, outboxRow, cancellationToken);
        var first = await solution.ReadInboxRowsAsync(cancellationToken);

        // Act
        await DispatchAsync(solution, outboxRow, cancellationToken);

        // Assert
        var second = await solution.ReadInboxRowsAsync(cancellationToken);
        Assert.Equal(first.Select(row => row.EventId), second.Select(row => row.EventId));
    }

    [Theory]
    [InlineData(typeof(PaymentsContext))]
    [InlineData(typeof(Auditing.AuditContext))]
    public async Task A_consumer_whose_row_exists_leaves_the_others_to_add_theirs(Type existing)
    {
        // Arrange
        // A redelivery after a partial fan-out: one Consumer added its row, the attempt then failed.
        // Run once per Consumer, so whichever the mediator calls first is the one already done.
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        var outboxRow = await PublishAsync(solution, cancellationToken);
        await DispatchAsync(solution, outboxRow, cancellationToken);
        await solution.DeleteInboxRowsAsync(
            row => !EF.Functions.Like(row.GroupKey, existing.Name + ":%"), cancellationToken);

        // Act
        await DispatchAsync(solution, outboxRow, cancellationToken);

        // Assert
        Assert.Equal(2, (await solution.ReadInboxRowsAsync(cancellationToken)).Count);
    }

    [Fact]
    public async Task A_failing_forwarder_retries_the_row_and_each_consumer_ends_with_one_row()
    {
        // Arrange
        var broker = new Forwarding.Broker { Down = true };
        await using var solution = SolutionUnderTest.Start(app => app.Services.AddSingleton(broker));
        var cancellationToken = TestContext.Current.CancellationToken;
        var outboxRow = await PublishAsync(solution, cancellationToken);
        await solution.DeliverAsync(cancellationToken);

        // Act
        broker.Down = false;
        await DispatchAsync(solution, outboxRow, cancellationToken);

        // Assert
        var retried = Assert.Single(await solution.ReadOutboxRowsAsync(cancellationToken));
        Assert.Equal(1, retried.RetryCount);
        Assert.Single(broker.Sent);
        Assert.Equal(2, (await solution.ReadInboxRowsAsync(cancellationToken)).Count);
    }

    [Fact]
    public async Task A_forwarder_receives_the_event_beside_the_consumers()
    {
        // Arrange
        var broker = new Forwarding.Broker();
        await using var solution = SolutionUnderTest.Start(app => app.Services.AddSingleton(broker));
        var cancellationToken = TestContext.Current.CancellationToken;
        await PublishAsync(solution, cancellationToken);

        // Act
        await solution.DeliverAsync(cancellationToken);

        // Assert
        Assert.Single(broker.Sent);
        Assert.Equal(2, (await solution.ReadInboxRowsAsync(cancellationToken)).Count);
    }

    private static async Task<OutboxMessage> PublishAsync(SolutionUnderTest solution, CancellationToken cancellationToken)
    {
        await solution.SendAsync(new AddSomeEntityCommand("a name", 12.34m), cancellationToken);
        return Assert.Single(await solution.ReadOutboxRowsAsync(cancellationToken));
    }

    /// <summary>Hands a row to its handler in a scope of its own, as the outbox worker does.</summary>
    private static async Task DispatchAsync(SolutionUnderTest solution, OutboxMessage row, CancellationToken cancellationToken)
    {
        using var scope = solution.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<HandlerRegistry<OutboxMessage>>();
        Assert.True(registry.TryGetEntry(row.Type, out var entry));
        await entry.HandleAsync(
            scope.ServiceProvider, row, new MessageMetadata(row.EventId, row.GroupKey), cancellationToken);
    }
}

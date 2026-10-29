using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.Features.Payments.Infrastructure.InboxHandlers;
using ModulithTemplate.SharedKernel.Outbox.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// The Consumer's half: each Consumer's inbox row is applied once, by a Command of its own, inside the
/// inbox's transaction. Every <see cref="SolutionUnderTest"/> has two Consumers, Payments and Auditing.
/// </summary>
public class IntegrationEventDeliveryTests
{
    [Fact]
    public async Task The_generator_registers_a_consumers_transport_under_its_envelopes_type_name()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();

        // Act
        using var scope = solution.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<HandlerRegistry<InboxMessage>>();

        // Assert
        // A subclass of the abstract generic handler is registered under the closed envelope's name,
        // the very string a staged row stores.
        var envelope = typeof(InboxEnvelope<PaymentsContext, SomeEntityAddedIntegrationEvent>).FullName!;
        Assert.True(registry.TryGetEntry(envelope, out var entry));
        Assert.Equal(typeof(SomeEntityAddedInboxHandler), entry.HandlerType);
    }

    [Fact]
    public async Task A_published_event_is_applied_by_its_consumer_and_its_row_completed()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        await solution.SendAsync(new AddSomeEntityCommand("a name", 12.34m), cancellationToken);
        var entity = Assert.Single(await solution.ReadSomeEntitiesAsync(cancellationToken));

        // Act
        await solution.DeliverAsync(cancellationToken);

        // Assert
        var payment = Assert.Single(await solution.ReadPaymentsAsync(cancellationToken));
        Assert.Equal(entity.Id, payment.OrderId);
        Assert.Equal(12.34m, payment.Amount);
        Assert.All(await solution.ReadInboxRowsAsync(cancellationToken), row => Assert.NotNull(row.CompletedAt));
    }

    [Fact]
    public async Task A_failing_consumer_leaves_the_other_consumer_committed_and_none_of_its_own_writes()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        await solution.SendAsync(new AddSomeEntityCommand(Auditing.Refused, 12.34m), cancellationToken);

        // Act
        await solution.DeliverAsync(cancellationToken);

        // Assert
        var rows = await solution.ReadInboxRowsAsync(cancellationToken);
        var payments = Assert.Single(rows, row => row.GroupKey.StartsWith("PaymentsContext:", StringComparison.Ordinal));
        var audit = Assert.Single(rows, row => row.GroupKey.StartsWith("AuditContext:", StringComparison.Ordinal));

        Assert.Single(await solution.ReadPaymentsAsync(cancellationToken));
        Assert.NotNull(payments.CompletedAt);

        // Refused after it wrote: the write went with its savepoint, and the row waits to be retried.
        Assert.Null(audit.CompletedAt);
        Assert.Equal(1, audit.RetryCount);
        Assert.Empty(await solution.ReadAuditEntriesAsync(cancellationToken));
    }

    [Fact]
    public async Task A_transient_failure_after_the_consumers_writes_replays_the_attempt_and_they_land_once()
    {
        // Arrange
        // The first completion fails after its Consumer has written: had those writes committed on their
        // own, the replay would record them twice.
        var completion = new TransientFailureInjector(failingCommand: 1, matching: "SET completed_at");
        await using var solution = SolutionUnderTest.Start(app => app.Services
            .ConfigureContext<InboxContext>(options => options
                .UseNpgsql(pg => pg.EnableRetryOnFailure(1, TimeSpan.FromMilliseconds(10), errorCodesToAdd: null))
                .AddInterceptors(completion))
            .ConfigureContext<PaymentsContext>(options => options
                .UseNpgsql(pg => pg.EnableRetryOnFailure(1, TimeSpan.FromMilliseconds(10), errorCodesToAdd: null))));
        var cancellationToken = TestContext.Current.CancellationToken;
        await solution.SendAsync(new AddSomeEntityCommand("a name", 12.34m), cancellationToken);

        // Act
        await solution.DeliverAsync(cancellationToken);

        // Assert
        Assert.Equal(3, completion.Matched);
        Assert.Single(await solution.ReadPaymentsAsync(cancellationToken));
        Assert.Single(await solution.ReadAuditEntriesAsync(cancellationToken));
        Assert.All(await solution.ReadInboxRowsAsync(cancellationToken), row =>
        {
            Assert.NotNull(row.CompletedAt);
            Assert.Equal(0, row.RetryCount);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Domain.Entities;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Outbox.Events;

using Underground.Outbox.Data;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// The claim the whole design rests on: an integration event's inbox rows are written by the same save
/// that persists the aggregate, so they exist if and only if the change they describe committed.
/// </summary>
/// <remarks>
/// Driven through the real command, mediator pipeline, dispatch interceptor, translating handler and
/// publisher, because the subtlety lives in how those meet at the save boundary.
/// </remarks>
public class InboxAtomicityTests
{
    private static AddSomeEntityCommand ACommand() => new("a name", 12.34m);

    [Fact]
    public async Task One_save_writes_the_aggregate_and_a_row_for_its_consumer()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var result = await solution.SendAsync(ACommand(), cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);

        // The crux: one save, and both rows were in it. Two saves would mean the event could survive
        // a failure of the change it describes, or the other way round.
        var save = Assert.Single(solution.OrdersSaves.Saves);
        Assert.Contains((nameof(SomeEntity), EntityState.Added), save);
        Assert.Contains((nameof(InboxMessage), EntityState.Added), save);

        // Payments is the only Consumer; Orders, which handles nothing, gets no row of its own.
        var row = Assert.Single(await solution.InboxRowsAsync(cancellationToken));
        Assert.Equal(
            typeof(InboxEnvelope<PaymentsContext, SomeEntityAddedIntegrationEvent>).FullName,
            row.Type,
            StringComparer.Ordinal);
        Assert.Empty(await solution.OutboxRowsAsync(cancellationToken));
    }

    [Fact]
    public async Task The_row_is_grouped_by_its_consumer_and_the_aggregate()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await solution.SendAsync(ACommand(), cancellationToken);

        // Assert
        // Ordered per aggregate, and stalled per Consumer: another Consumer's stuck row for the same
        // aggregate sits in a Group of its own.
        var entity = Assert.Single(await solution.SomeEntitiesAsync(cancellationToken));
        var row = Assert.Single(await solution.InboxRowsAsync(cancellationToken));
        Assert.Equal($"PaymentsContext:{entity.Id}", row.GroupKey, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Publishing_the_same_event_again_stages_the_same_row_identity()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var integrationEvent = new SomeEntityAddedIntegrationEvent(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "a name", 12.34m);

        // Act
        // What a replayed save does: publish the same event into a fresh change tracker.
        var first = StagedRow(solution, integrationEvent);
        var second = StagedRow(solution, integrationEvent);

        // Assert
        // The same id, so the inbox's unique index refuses a second copy rather than storing one.
        Assert.Equal(first.EventId, second.EventId);
        Assert.NotEqual(integrationEvent.EventId, first.EventId);

        static InboxMessage StagedRow(SolutionUnderTest solution, SomeEntityAddedIntegrationEvent integrationEvent)
        {
            using var scope = solution.CreateScope();
            scope.ServiceProvider.GetRequiredService<IOrdersIntegrationEventPublisher>().Publish(integrationEvent);
            return Assert.Single(scope.ServiceProvider.GetRequiredService<OrdersContext>()
                .ChangeTracker.Entries<InboxMessage>()).Entity;
        }
    }

    [Fact]
    public async Task Nothing_is_written_when_the_save_fails()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start(app => app.Services.ConfigureContext<OrdersContext>(
            options => options.AddInterceptors(new TransientFailureInjector(failingCommand: 1, matching: "inbox"))));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var result = await solution.SendAsync(ACommand(), cancellationToken);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Empty(await solution.SomeEntitiesAsync(cancellationToken));
        Assert.Empty(await solution.InboxRowsAsync(cancellationToken));
    }

    [Fact]
    public async Task Nothing_is_written_when_the_save_is_not_performed()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = solution.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<OrdersContext>();
        var entity = SomeEntity.Create("a name", 12.34m);

        // Act
        // The publisher's whole contract: it stages, and the caller's next save is what writes.
        orders.Add(entity);
        scope.ServiceProvider.GetRequiredService<IOrdersIntegrationEventPublisher>()
            .Publish(new SomeEntityAddedIntegrationEvent(
                Guid.CreateVersion7(), entity.Id, entity.Name, entity.Amount));

        // Assert
        Assert.Single(orders.ChangeTracker.Entries<InboxMessage>());
        Assert.Empty(await solution.InboxRowsAsync(cancellationToken));
        Assert.Empty(solution.OrdersSaves.Saves);
    }
}

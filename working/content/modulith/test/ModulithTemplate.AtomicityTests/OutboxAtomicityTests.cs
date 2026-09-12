using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Domain.Entities;
using ModulithTemplate.Features.Orders.Infrastructure.Data;

using Underground.Outbox.Data;

namespace ModulithTemplate.AtomicityTests;

/// <summary>
/// The claim the whole design rests on: an integration event is recorded by the same save that
/// persists the aggregate, so it exists if and only if the change it describes committed.
/// </summary>
/// <remarks>
/// Driven through the real command, mediator pipeline, dispatch interceptor, translating handler and
/// publisher, because the subtlety lives in how those meet at the save boundary. A substitute could
/// only say that a method was called, which is an implementation detail rather than the behaviour.
/// </remarks>
public class OutboxAtomicityTests
{
    private static AddSomeEntityCommand ACommand() => new("a name", 12.34m);

    [Fact]
    public async Task One_save_writes_the_aggregate_and_its_integration_event()
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
        Assert.Contains((nameof(OutboxMessage), EntityState.Added), save);

        var staged = Assert.Single(await solution.OutboxRowsAsync(cancellationToken));
        Assert.Equal(typeof(SomeEntityAddedIntegrationEvent).FullName, staged.Type, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_staged_event_carries_the_aggregate_as_its_group_key()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await solution.SendAsync(ACommand(), cancellationToken);

        // Assert
        // Events about one aggregate are delivered in order; unrelated aggregates proceed
        // concurrently. A publish site cannot supply this, so the only way it can be wrong is here.
        using var scope = solution.CreateScope();
        var entity = await scope.ServiceProvider.GetRequiredService<OrdersContext>()
            .Set<SomeEntity>().AsNoTracking().SingleAsync(cancellationToken);
        var staged = Assert.Single(await solution.OutboxRowsAsync(cancellationToken));
        Assert.Equal(entity.Id.ToString(), staged.GroupKey, StringComparer.Ordinal);
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
        // The publisher's whole contract: it stages, and the caller's next save is what writes. No
        // save follows here, which stands in for the save that fails or is rolled back.
        orders.Add(entity);
        scope.ServiceProvider.GetRequiredService<IOrdersIntegrationEventPublisher>()
            .Publish(new SomeEntityAddedIntegrationEvent(
                Guid.CreateVersion7(), entity.Id, entity.Name, entity.Amount));

        // Assert
        Assert.Single(orders.ChangeTracker.Entries<OutboxMessage>());
        Assert.Empty(await solution.OutboxRowsAsync(cancellationToken));
        Assert.Empty(solution.OrdersSaves.Saves);
    }
}

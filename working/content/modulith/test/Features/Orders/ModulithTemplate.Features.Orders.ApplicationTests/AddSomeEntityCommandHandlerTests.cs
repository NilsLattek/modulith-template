using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Contracts.IntegrationEvents;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Tests for <see cref="AddSomeEntityCommandHandler"/>.</summary>
public class AddSomeEntityCommandHandlerTests
{
    [Fact]
    public async Task Handle_adds_the_entity_to_the_repository_and_returns_a_successful_result()
    {
        // Arrange
        var repository = Substitute.For<IOrdersRepository<SomeEntity>>();
        var handler = new AddSomeEntityCommandHandler(repository, Substitute.For<IIntegrationEventQueue>());

        // Act
        var result = await handler.Handle(new AddSomeEntityCommand("a name"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        await repository.Received(1).AddAsync(Arg.Any<SomeEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_enqueues_the_integration_event_rather_than_publishing_it()
    {
        // Arrange
        var events = Substitute.For<IIntegrationEventQueue>();
        var handler = new AddSomeEntityCommandHandler(Substitute.For<IOrdersRepository<SomeEntity>>(), events);

        // Act
        await handler.Handle(new AddSomeEntityCommand("a name"), TestContext.Current.CancellationToken);

        // Assert
        events.Received(1).Enqueue(Arg.Is<SomeEntityAddedIntegrationEvent>(e =>
            string.Equals(e.Name, "a name", StringComparison.Ordinal)));

        // Dispatch is the host's job, not the handler's: flushing here would publish before the
        // surrounding operation had a chance to fail.
        await events.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
    }
}

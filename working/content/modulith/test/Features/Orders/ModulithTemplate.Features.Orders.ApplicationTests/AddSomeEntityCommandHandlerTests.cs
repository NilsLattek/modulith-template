using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
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
        var handler = new AddSomeEntityCommandHandler(repository);

        // Act
        var result = await handler.Handle(new AddSomeEntityCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        await repository.Received(1).AddAsync(Arg.Any<SomeEntity>(), Arg.Any<CancellationToken>());
    }
}

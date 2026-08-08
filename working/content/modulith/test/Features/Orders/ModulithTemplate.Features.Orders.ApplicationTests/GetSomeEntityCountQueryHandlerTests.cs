using ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Tests for <see cref="GetSomeEntityCountQueryHandler"/>.</summary>
public class GetSomeEntityCountQueryHandlerTests
{
    [Fact]
    public async Task Handle_with_entities_in_the_repository_returns_a_successful_result_carrying_the_count()
    {
        // Arrange
        var repository = Substitute.For<IOrdersRepository<SomeEntity>>();
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(3);
        var handler = new GetSomeEntityCountQueryHandler(repository);

        // Act
        var result = await handler.Handle(new GetSomeEntityCountQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
    }
}

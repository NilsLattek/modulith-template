using ModulithTemplate.Features.Orders.Application.Api;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Tests for <see cref="OrdersApi"/>.</summary>
public class OrdersApiTests
{
    [Fact]
    public async Task GetSummaryAsync_returns_the_entity_count_from_the_repository()
    {
        // Arrange
        var repository = Substitute.For<IOrdersRepository<SomeEntity>>();
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(3);
        var api = new OrdersApi(repository);

        // Act
        var summary = await api.GetSummaryAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, summary.EntityCount);
    }
}

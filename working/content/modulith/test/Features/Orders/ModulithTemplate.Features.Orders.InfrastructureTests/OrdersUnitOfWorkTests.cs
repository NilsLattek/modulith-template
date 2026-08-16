using Mediator;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Infrastructure;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.InfrastructureTests;

/// <summary>Tests for the Orders feature's unit of work.</summary>
/// <remarks>
/// Asserts the <i>registration</i>, not the transaction: a feature that declares
/// <see cref="IOrdersUnitOfWork"/> but forgets the DI line fails only at its first transactional
/// handler.
/// </remarks>
public class OrdersUnitOfWorkTests
{
    /// <summary>
    /// The unit of work must resolve through the feature's own interface, bound to the feature's
    /// own context.
    /// </summary>
    [Fact]
    public void ConfigureOrdersInfrastructure_registers_the_feature_unit_of_work()
    {
        // Arrange — a syntactically valid connection string is required; nothing connects to it.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:PostgresConnection"] = "Host=localhost;Database=modulith_tests",
            })
            .Build();

        // Act — logging and IPublisher stand in for what the host supplies (AddLogging, AddMediator).
        // The context's domain event interceptor needs both; a feature's composition root has neither.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IPublisher>());

        using var provider = services
            .ConfigureOrdersInfrastructure(configuration)
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOrdersUnitOfWork>());
    }
}

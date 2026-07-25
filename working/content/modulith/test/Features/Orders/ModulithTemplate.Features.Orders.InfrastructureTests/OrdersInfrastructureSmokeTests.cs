using Microsoft.EntityFrameworkCore;

using ModulithTemplate.Features.Orders.Infrastructure.Data;

namespace ModulithTemplate.Features.Orders.InfrastructureTests;

/// <summary>Smoke tests for the Orders infrastructure layer.</summary>
public class OrdersInfrastructureSmokeTests
{
    /// <summary>
    /// Every feature owns its schema, set via <c>HasDefaultSchema</c> in <c>OnModelCreating</c>.
    /// Building the model is enough to assert it — Npgsql does not open a connection to do so,
    /// which keeps this a unit test with no database dependency.
    /// </summary>
    [Fact]
    public void OrdersContext_model_defaults_to_the_orders_schema()
    {
        // Arrange — a syntactically valid connection string is required; nothing connects to it.
        var options = new DbContextOptionsBuilder<OrdersContext>()
            .UseNpgsql("Host=localhost;Database=modulith_tests")
            .Options;

        // Act
        using var context = new OrdersContext(options);
        var defaultSchema = context.Model.GetDefaultSchema();

        // Assert
        Assert.Equal("orders", defaultSchema);
    }
}

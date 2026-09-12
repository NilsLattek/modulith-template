using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using ModulithTemplate.Features.Payments.Domain.Entities;
using ModulithTemplate.Features.Payments.Infrastructure.Data;

namespace ModulithTemplate.Features.Payments.InfrastructureTests;

/// <summary>Tests the mapping of <see cref="Payment"/> onto the Payments schema.</summary>
/// <remarks>
/// Building the model is enough — Npgsql opens no connection to do so, so this stays a unit test.
/// </remarks>
public class PaymentConfigurationTests
{
    private static IModel BuildModel()
    {
        // A syntactically valid connection string is required; nothing connects to it.
        var options = new DbContextOptionsBuilder<PaymentsContext>()
            .UseNpgsql("Host=localhost;Database=modulith_tests")
            .Options;

        using var context = new PaymentsContext(options);
        return context.Model;
    }

    /// <summary>
    /// The entity must be discovered by <c>ApplyConfigurationsFromAssembly</c> and land in the
    /// feature's own schema; an unmapped entity would fail only when a migration is generated.
    /// </summary>
    [Fact]
    public void Payment_is_mapped_into_the_payments_schema()
    {
        // Act
        var entityType = BuildModel().FindEntityType(typeof(Payment));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("payments", entityType.GetSchema(), StringComparer.Ordinal);
    }

    /// <summary>
    /// The amount column must store exactly the scale the entity enforces, so a value the entity
    /// accepted cannot be rounded on its way into the database.
    /// </summary>
    [Fact]
    public void Amount_column_stores_the_scale_the_entity_enforces()
    {
        // Act
        var amount = BuildModel().FindEntityType(typeof(Payment))!.FindProperty(nameof(Payment.Amount))!;

        // Assert
        Assert.Equal(Payment.AmountScale, amount.GetScale());
        Assert.Equal(18, amount.GetPrecision());
    }
}

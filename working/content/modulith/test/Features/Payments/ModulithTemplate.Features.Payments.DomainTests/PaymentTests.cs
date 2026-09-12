using ModulithTemplate.Features.Payments.Domain.Entities;

namespace ModulithTemplate.Features.Payments.DomainTests;

/// <summary>Tests for <see cref="Payment"/>.</summary>
/// <remarks>
/// An invariant is tested on the entity, not through a handler: it must hold for every caller, and
/// this is where that guarantee lives.
/// </remarks>
public class PaymentTests
{
    private static readonly Guid AnOrderId = Guid.CreateVersion7();

    [Fact]
    public void Record_assigns_an_identity_and_keeps_the_order_and_amount()
    {
        // Act
        var payment = Payment.Record(AnOrderId, 12.34m);

        // Assert
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(AnOrderId, payment.OrderId);
        Assert.Equal(12.34m, payment.Amount);
    }

    [Fact]
    public void Record_rejects_an_empty_order_id() =>
        Assert.Throws<ArgumentException>(() => Payment.Record(Guid.Empty, 1m));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Record_rejects_a_non_positive_amount(decimal amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Payment.Record(AnOrderId, amount));

    [Fact]
    public void Record_rejects_an_amount_past_the_column_scale() =>
        Assert.Throws<ArgumentException>(() => Payment.Record(AnOrderId, 1.234m));

    [Fact]
    public void Record_rejects_an_amount_past_the_column_precision() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Payment.Record(AnOrderId, Payment.MaxAmount + 0.01m));

    [Fact]
    public void Record_accepts_the_largest_storable_amount() =>
        Assert.Equal(Payment.MaxAmount, Payment.Record(AnOrderId, Payment.MaxAmount).Amount);
}

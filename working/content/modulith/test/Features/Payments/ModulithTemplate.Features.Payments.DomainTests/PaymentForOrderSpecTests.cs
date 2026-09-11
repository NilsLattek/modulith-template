using ModulithTemplate.Features.Payments.Domain.Entities;
using ModulithTemplate.Features.Payments.Domain.Specifications;

namespace ModulithTemplate.Features.Payments.DomainTests;

/// <summary>Tests for <see cref="PaymentForOrderSpec"/>.</summary>
/// <remarks>
/// Evaluated in memory, so the criteria are asserted without a database. Worth pinning: a
/// specification that accidentally declares no criteria matches every row, and the consumer that
/// uses this one would then treat any existing payment as proof it had already run.
/// </remarks>
public class PaymentForOrderSpecTests
{
    private static readonly Guid WantedOrder = Guid.CreateVersion7();
    private static readonly Guid OtherOrder = Guid.CreateVersion7();

    private static readonly Payment[] Payments =
    [
        Payment.Record(WantedOrder, 10m),
        Payment.Record(OtherOrder, 20m),
    ];

    [Fact]
    public void It_selects_only_the_payments_for_that_order()
    {
        // Act
        var matched = new PaymentForOrderSpec(WantedOrder).Evaluate(Payments).ToList();

        // Assert
        Assert.Equal(WantedOrder, Assert.Single(matched).OrderId);
    }

    [Fact]
    public void It_selects_nothing_for_an_order_with_no_payment()
    {
        // Act
        var matched = new PaymentForOrderSpec(Guid.CreateVersion7()).Evaluate(Payments);

        // Assert
        Assert.Empty(matched);
    }
}

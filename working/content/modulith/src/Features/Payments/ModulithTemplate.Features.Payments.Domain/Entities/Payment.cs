namespace ModulithTemplate.Features.Payments.Domain.Entities;

/// <summary>
/// A payment recorded against an order. The order is referenced by id only — Payments owns no part
/// of the Orders model, and the two features share no types.
/// </summary>
public sealed class Payment
{
    public Guid Id { get; private set; }

    /// <summary>The order this payment settles, as published by the Orders feature.</summary>
    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Decimal places the amount column stores; a finer amount would be silently rounded.</summary>
    public const int AmountScale = 2;

    /// <summary>Total significant digits the amount column stores.</summary>
    public const int AmountPrecision = 18;

    /// <summary>The largest amount the column holds: 16 integral digits, then <see cref="AmountScale"/> more.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>Records a payment against an order.</summary>
    /// <param name="orderId">The order being paid.</param>
    /// <param name="amount">The amount paid.</param>
    /// <returns>The new payment.</returns>
    /// <exception cref="ArgumentException"><paramref name="orderId"/> is empty, or <paramref name="amount"/> is finer than the stored scale.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive, or exceeds <see cref="MaxAmount"/>.</exception>
    public static Payment Record(Guid orderId, decimal amount) => new(orderId, amount);

    /// <summary>Materialises an instance from the database.</summary>
    /// <remarks>Only EF Core uses this; it bypasses the invariants, so nothing else may.</remarks>
    private Payment()
    {
    }

    /// <summary>Records a payment, enforcing its invariants.</summary>
    /// <remarks><c>internal</c>, so Application can only reach an instance through the factory.</remarks>
    /// <param name="orderId">The order being paid.</param>
    /// <param name="amount">The amount paid.</param>
    internal Payment(Guid orderId, decimal amount)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("An order id is required.", nameof(orderId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);

        // Checked here rather than left to the database, where it surfaces only at SaveChanges as a
        // Postgres numeric overflow, long after the caller that could have handled it.
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amount, MaxAmount);

        // Rejected rather than rounded: silently altering an amount is worse than refusing it.
        if (decimal.Round(amount, AmountScale) != amount)
        {
            throw new ArgumentException($"An amount may have at most {AmountScale} decimal places.", nameof(amount));
        }

        Id = Guid.CreateVersion7();
        OrderId = orderId;
        Amount = amount;
    }
}

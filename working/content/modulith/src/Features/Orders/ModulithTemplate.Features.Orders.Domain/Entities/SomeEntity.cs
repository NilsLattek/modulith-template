using ModulithTemplate.Features.Orders.Domain.Events;
using ModulithTemplate.SharedKernel.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Domain.Entities;

/// <summary>
/// A placeholder aggregate, mapped so the scaffold has something real to read and write; delete it
/// once the feature has entities of its own.
/// </summary>
public sealed class SomeEntity : AggregateRoot
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>What this entity is worth, announced to siblings when it is created.</summary>
    public decimal Amount { get; private set; }

    public const int NameMaxLength = 200;

    /// <summary>Decimal places the amount column stores; a finer amount would be silently rounded.</summary>
    public const int AmountScale = 2;

    /// <summary>Total significant digits the amount column stores.</summary>
    public const int AmountPrecision = 18;

    /// <summary>The largest amount the column holds: 16 integral digits, then <see cref="AmountScale"/> more.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>Creates an entity.</summary>
    /// <param name="name">The name to give it.</param>
    /// <param name="amount">The amount it is worth.</param>
    /// <returns>The new entity.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or too long, or <paramref name="amount"/> is finer than the stored scale.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive, or exceeds <see cref="MaxAmount"/>.</exception>
    public static SomeEntity Create(string name, decimal amount) => new(name, amount);

    /// <summary>Materialises an instance from the database.</summary>
    /// <remarks>Only EF Core uses this; it bypasses the invariants, so nothing else may.</remarks>
    private SomeEntity()
    {
    }

    /// <summary>Creates an entity, enforcing its invariants.</summary>
    /// <remarks><c>internal</c>, so Application can only reach an instance through the factory.</remarks>
    /// <param name="name">The name to give it.</param>
    /// <param name="amount">The amount it is worth.</param>
    internal SomeEntity(string name, decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException($"A name may be at most {NameMaxLength} characters.", nameof(name));
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
        Name = trimmed;
        Amount = amount;

        // Last, so the event describes something that did happen: every invariant above has held.
        RaiseDomainEvent(new SomeEntityAddedDomainEvent(Id, Name, Amount));
    }
}

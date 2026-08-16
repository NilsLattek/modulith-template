namespace ModulithTemplate.Features.Orders.Domain.Entities;

/// <summary>
/// A placeholder entity, mapped so the scaffold has something real to read and write; delete it
/// once the feature has entities of its own.
/// </summary>
public sealed class SomeEntity
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public const int NameMaxLength = 200;

    /// <summary>Creates an entity.</summary>
    /// <param name="name">The name to give it.</param>
    /// <returns>The new entity.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or too long.</exception>
    public static SomeEntity Create(string name) => new(name);

    /// <summary>Materialises an instance from the database.</summary>
    /// <remarks>Only EF Core uses this; it bypasses the invariants, so nothing else may.</remarks>
    private SomeEntity()
    {
    }

    /// <summary>Creates an entity, enforcing its invariants.</summary>
    /// <remarks><c>internal</c>, so Application can only reach an instance through the factory.</remarks>
    /// <param name="name">The name to give it.</param>
    internal SomeEntity(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException($"A name may be at most {NameMaxLength} characters.", nameof(name));
        }

        Id = Guid.CreateVersion7();
        Name = trimmed;
    }
}

using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.DomainTests;

/// <summary>Tests for <see cref="SomeEntity"/>.</summary>
/// <remarks>
/// An invariant is tested on the entity, not through a handler: it must hold for every caller, and
/// this is where that guarantee lives.
/// </remarks>
public class SomeEntityTests
{
    [Fact]
    public void Create_assigns_an_identity_and_the_trimmed_name()
    {
        // Act
        var entity = SomeEntity.Create("  a name  ");

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("a name", entity.Name, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string name) =>
        Assert.Throws<ArgumentException>(() => SomeEntity.Create(name));

    [Fact]
    public void Create_rejects_a_name_past_the_column_length() =>
        Assert.Throws<ArgumentException>(() => SomeEntity.Create(new string('a', SomeEntity.NameMaxLength + 1)));
}

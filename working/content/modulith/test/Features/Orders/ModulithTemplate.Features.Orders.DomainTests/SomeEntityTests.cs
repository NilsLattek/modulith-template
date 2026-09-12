using ModulithTemplate.Features.Orders.Domain.Entities;
using ModulithTemplate.Features.Orders.Domain.Events;

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
        var entity = SomeEntity.Create("  a name  ", 12.34m);

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("a name", entity.Name, StringComparer.Ordinal);
        Assert.Equal(12.34m, entity.Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string name) =>
        Assert.Throws<ArgumentException>(() => SomeEntity.Create(name, 1m));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_amount(int amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SomeEntity.Create("a name", amount));

    [Fact]
    public void Create_rejects_an_amount_past_the_column_capacity() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SomeEntity.Create("a name", SomeEntity.MaxAmount + 1m));

    [Fact]
    public void Create_rejects_an_amount_finer_than_the_stored_scale() =>
        Assert.Throws<ArgumentException>(() => SomeEntity.Create("a name", 1.005m));

    [Fact]
    public void Create_raises_the_added_domain_event_carrying_the_entity()
    {
        // Act
        var entity = SomeEntity.Create("a name", 12.34m);

        // Assert
        var raised = Assert.IsType<SomeEntityAddedDomainEvent>(Assert.Single(entity.DomainEvents));
        Assert.Equal(entity.Id, raised.SomeEntityId);
        Assert.Equal("a name", raised.Name, StringComparer.Ordinal);
        Assert.Equal(12.34m, raised.Amount);
    }

    [Fact]
    public void Create_raises_no_event_when_an_invariant_is_broken()
    {
        // An event says something happened, so a constructor that throws must leave none behind —
        // here that is structural, since the entity itself never escapes.
        Assert.Throws<ArgumentException>(() => SomeEntity.Create("", 1m));
    }

    [Fact]
    public void Create_rejects_a_name_past_the_column_length() =>
        Assert.Throws<ArgumentException>(() => SomeEntity.Create(new string('a', SomeEntity.NameMaxLength + 1), 1m));
}

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Tests for <see cref="AddSomeEntityCommandValidator"/>.</summary>
public class AddSomeEntityCommandValidatorTests
{
    [Fact]
    public void Validate_with_a_name_within_the_length_limit_succeeds()
    {
        // Arrange
        var validator = new AddSomeEntityCommandValidator();

        // Act
        var result = validator.Validate(new AddSomeEntityCommand("a name", 12.34m));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_with_an_empty_name_fails()
    {
        // Arrange
        var validator = new AddSomeEntityCommandValidator();

        // Act
        var result = validator.Validate(new AddSomeEntityCommand("", 12.34m));

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(nameof(AddSomeEntityCommand.Name), Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Validate_with_a_name_over_the_length_limit_fails()
    {
        // Arrange
        var validator = new AddSomeEntityCommandValidator();

        // Act
        var result = validator.Validate(new AddSomeEntityCommand(new string('a', 201), 12.34m));

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_with_a_non_positive_amount_fails(int amount)
    {
        // Arrange
        var validator = new AddSomeEntityCommandValidator();

        // Act
        var result = validator.Validate(new AddSomeEntityCommand("a name", amount));

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(nameof(AddSomeEntityCommand.Amount), Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Validate_with_an_amount_finer_than_the_stored_scale_fails()
    {
        // Arrange
        var validator = new AddSomeEntityCommandValidator();

        // Act
        var result = validator.Validate(new AddSomeEntityCommand("a name", 1.005m));

        // Assert
        Assert.False(result.IsValid);
    }
}

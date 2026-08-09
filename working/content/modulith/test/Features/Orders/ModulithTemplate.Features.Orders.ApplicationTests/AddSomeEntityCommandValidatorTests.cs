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
        var result = validator.Validate(new AddSomeEntityCommand("a name"));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_with_an_empty_name_fails()
    {
        // Arrange
        var validator = new AddSomeEntityCommandValidator();

        // Act
        var result = validator.Validate(new AddSomeEntityCommand(""));

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
        var result = validator.Validate(new AddSomeEntityCommand(new string('a', 201)));

        // Assert
        Assert.False(result.IsValid);
    }
}

using ModulithTemplate.SharedKernel.Domain.Exceptions;

namespace ModulithTemplate.SharedKernel.DomainTests;

/// <summary>Tests for <see cref="BusinessException"/>.</summary>
public class BusinessExceptionTests
{
    [Fact]
    public void Constructor_keeps_the_code_and_the_message()
    {
        // Act
        var exception = new BusinessException("Orders:Refused", "Refused.");

        // Assert
        Assert.Equal("Orders:Refused", exception.Code);
        Assert.Equal("Refused.", exception.Message);
        Assert.Empty(exception.Parameters);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_with_a_blank_code_throws(string code)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new BusinessException(code, "Refused."));
    }

    [Fact]
    public void Constructor_with_a_blank_message_throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new BusinessException("Orders:Refused", " "));
    }

    [Fact]
    public void WithParameter_records_the_value_and_returns_the_same_exception()
    {
        // Arrange
        var exception = new BusinessException("Orders:NameAlreadyTaken", "The name is already taken.");

        // Act
        var returned = exception.WithParameter("Name", "Widget");

        // Assert
        Assert.Same(exception, returned);
        Assert.Equal("Widget", exception.Parameters["Name"]);
    }
}

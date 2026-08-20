using FluentResults;

using FluentValidation;

using Mediator;

using ModulithTemplate.Web.Behaviours;
using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="ValidationBehaviour{TMessage, TResponse}"/>.</summary>
public class ValidationBehaviourTests
{
    /// <summary>Stand-in message type with a property for the validators below to check.</summary>
    public sealed record TestCommand(string Name) : ICommand<Result>;

    /// <summary>Stand-in message type for the generic <see cref="Result{TValue}"/> case.</summary>
    public sealed record TestQuery(int Page) : IQuery<Result<int>>;

    private sealed class NameNotEmptyValidator : AbstractValidator<TestCommand>
    {
        public NameNotEmptyValidator() => RuleFor(command => command.Name).NotEmpty().WithMessage("name is required");
    }

    private sealed class NameMinLengthValidator : AbstractValidator<TestCommand>
    {
        public NameMinLengthValidator() => RuleFor(command => command.Name).MinimumLength(3).WithMessage("name is too short");
    }

    private sealed class PagePositiveValidator : AbstractValidator<TestQuery>
    {
        public PagePositiveValidator() => RuleFor(query => query.Page).GreaterThan(0);
    }

    [Fact]
    public async Task Handle_when_no_validator_is_registered_calls_the_handler()
    {
        // Arrange
        var behaviour = new ValidationBehaviour<TestCommand, Result>([]);
        var handlerCalled = false;

        // Act
        var result = await behaviour.Handle(
            new TestCommand(""),
            (_, _) => { handlerCalled = true; return ValueTask.FromResult(Result.Ok()); },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handlerCalled);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_when_the_message_is_valid_calls_the_handler()
    {
        // Arrange
        var behaviour = new ValidationBehaviour<TestCommand, Result>([new NameNotEmptyValidator()]);
        var handlerCalled = false;

        // Act
        var result = await behaviour.Handle(
            new TestCommand("ok"),
            (_, _) => { handlerCalled = true; return ValueTask.FromResult(Result.Ok()); },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handlerCalled);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_when_a_rule_fails_returns_a_failed_result_without_calling_the_handler()
    {
        // Arrange
        var behaviour = new ValidationBehaviour<TestCommand, Result>([new NameNotEmptyValidator()]);
        var handlerCalled = false;

        // Act
        var result = await behaviour.Handle(
            new TestCommand(""),
            (_, _) => { handlerCalled = true; return ValueTask.FromResult(Result.Ok()); },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(handlerCalled);
        Assert.True(result.IsFailed);
        var error = Assert.IsType<ValidationError>(Assert.Single(result.Errors));
        Assert.Equal(nameof(TestCommand.Name), error.PropertyName);
        Assert.Equal(nameof(TestCommand.Name), error.Metadata[nameof(ValidationError.PropertyName)]);
    }

    [Fact]
    public async Task Handle_when_several_validators_fail_reports_every_failure()
    {
        // Arrange
        var behaviour = new ValidationBehaviour<TestCommand, Result>(
            [new NameNotEmptyValidator(), new NameMinLengthValidator()]);

        // Act — an empty name breaks both validators' rules
        var result = await behaviour.Handle(
            new TestCommand(""),
            (_, _) => ValueTask.FromResult(Result.Ok()),
            TestContext.Current.CancellationToken);

        // Assert — every validator runs; the behaviour does not stop at the first one that fails
        Assert.True(result.IsFailed);
        Assert.All(result.Errors, error => Assert.IsType<ValidationError>(error));
        Assert.Contains(result.Errors, error => string.Equals(error.Message, "name is required", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => string.Equals(error.Message, "name is too short", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_when_a_rule_fails_on_a_generic_result_response_returns_a_failed_result()
    {
        // Arrange
        var behaviour = new ValidationBehaviour<TestQuery, Result<int>>([new PagePositiveValidator()]);

        // Act
        var result = await behaviour.Handle(
            new TestQuery(0),
            (_, _) => ValueTask.FromResult(Result.Ok(42)),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Equal(nameof(TestQuery.Page), Assert.IsType<ValidationError>(Assert.Single(result.Errors)).PropertyName);
    }
}

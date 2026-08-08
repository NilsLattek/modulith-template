using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging.Abstractions;

using ModulithTemplate.Web.Behaviours;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="ExceptionBehaviour{TMessage, TResponse}"/>.</summary>
public class ExceptionBehaviourTests
{
    /// <summary>Stand-in message type; the behaviour never inspects the message itself.</summary>
    public sealed record TestQuery : IQuery<Result<int>>;

    /// <summary>Stand-in message type for the non-generic <see cref="Result"/> case.</summary>
    public sealed record TestCommand : ICommand<Result>;

    [Fact]
    public async Task Handle_when_the_handler_throws_returns_a_failed_result_carrying_the_exception()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestQuery, Result<int>>(NullLogger<ExceptionBehaviour<TestQuery, Result<int>>>.Instance);
        var boom = new InvalidOperationException("boom");

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => throw boom, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailed);
        var error = Assert.Single(result.Errors);
        Assert.Same(boom, Assert.IsType<ExceptionalError>(error).Exception);
    }

    [Fact]
    public async Task Handle_when_the_handler_throws_returns_a_failed_result_for_a_non_generic_result_response()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestCommand, Result>(NullLogger<ExceptionBehaviour<TestCommand, Result>>.Instance);

        // Act
        var result = await behaviour.Handle(new TestCommand(), (_, _) => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_when_the_handler_is_cancelled_rethrows_instead_of_converting()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestQuery, Result<int>>(NullLogger<ExceptionBehaviour<TestQuery, Result<int>>>.Instance);

        // Act
        var act = async () => await behaviour.Handle(new TestQuery(), (_, _) => throw new OperationCanceledException(), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task Handle_when_the_handler_succeeds_passes_the_result_through_untouched()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestQuery, Result<int>>(NullLogger<ExceptionBehaviour<TestQuery, Result<int>>>.Instance);
        var expected = Result.Ok(42);

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(expected), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
    }
}

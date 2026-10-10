using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

using ModulithTemplate.SharedKernel.Application.Behaviours;
using ModulithTemplate.SharedKernel.Application.Errors;
using ModulithTemplate.SharedKernel.Domain.Exceptions;

namespace ModulithTemplate.SharedKernel.ApplicationTests;

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
    public async Task Handle_when_a_business_rule_is_violated_returns_a_business_error_with_its_code_and_parameters()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestCommand, Result>(NullLogger<ExceptionBehaviour<TestCommand, Result>>.Instance);
        var violation = new BusinessException("Orders:NameAlreadyTaken", "The name 'Widget' is already taken.")
            .WithParameter("Name", "Widget");

        // Act
        var result = await behaviour.Handle(new TestCommand(), (_, _) => throw violation, TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.IsType<BusinessError>(Assert.Single(result.Errors));
        Assert.Equal("Orders:NameAlreadyTaken", error.Code);
        Assert.Equal("The name 'Widget' is already taken.", error.Message);
        Assert.Equal("Widget", error.Parameters["Name"]);
        Assert.Equal("Orders:NameAlreadyTaken", error.Metadata[nameof(BusinessError.Code)]);
    }

    [Fact]
    public async Task Handle_when_a_business_rule_is_violated_does_not_log_an_error()
    {
        // Arrange
        var logger = new FakeLogger<ExceptionBehaviour<TestCommand, Result>>();
        var behaviour = new ExceptionBehaviour<TestCommand, Result>(logger);

        // Act
        await behaviour.Handle(new TestCommand(), (_, _) => throw new BusinessException("Orders:Refused", "Refused."), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task Handle_when_a_change_was_made_from_a_stale_copy_returns_a_concurrency_error_without_logging_an_error()
    {
        // Arrange
        var logger = new FakeLogger<ExceptionBehaviour<TestCommand, Result>>();
        var behaviour = new ExceptionBehaviour<TestCommand, Result>(logger);

        // Act
        var result = await behaviour.Handle(new TestCommand(), (_, _) => throw new ConcurrencyException(), TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ConcurrencyError>(Assert.Single(result.Errors));
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task Handle_when_the_handler_throws_logs_the_exception_as_an_error()
    {
        // Arrange
        var logger = new FakeLogger<ExceptionBehaviour<TestCommand, Result>>();
        var behaviour = new ExceptionBehaviour<TestCommand, Result>(logger);
        var boom = new InvalidOperationException("boom");

        // Act
        await behaviour.Handle(new TestCommand(), (_, _) => throw boom, TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Same(boom, record.Exception);
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

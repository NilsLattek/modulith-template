using System.Diagnostics;

using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using ModulithTemplate.SharedKernel.Application;
using ModulithTemplate.SharedKernel.Application.Behaviours;
using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.SharedKernel.ApplicationTests;

/// <summary>Tests for <see cref="ExceptionBehaviour{TMessage, TResponse}"/>.</summary>
public class ExceptionBehaviourTests
{
    /// <summary>Stand-in message type; the behaviour never inspects the message itself.</summary>
    public sealed record TestQuery : IQuery<Result<int>>;

    /// <summary>Stand-in message type for the non-generic <see cref="Result"/> case.</summary>
    public sealed record TestCommand : ICommand<Result>;

    /// <summary>Recognises <see cref="TimeoutException"/> only.</summary>
    private sealed class TimeoutTranslator : IExceptionTranslator
    {
        public AppError? Translate(Exception exception) =>
            exception is TimeoutException ? new ConflictError("Test.TimedOut", "timed out") : null;
    }

    private static ExceptionBehaviour<TestQuery, Result<int>> CreateBehaviour(
        FakeLogger<ExceptionBehaviour<TestQuery, Result<int>>>? logger = null,
        params IExceptionTranslator[] translators) =>
        new(translators, logger ?? new FakeLogger<ExceptionBehaviour<TestQuery, Result<int>>>());

    [Fact]
    public async Task Handle_when_the_handler_throws_returns_an_unexpected_error_without_the_exception_text()
    {
        // Arrange
        var behaviour = CreateBehaviour();

        // Act
        var result = await behaviour.Handle(
            new TestQuery(), (_, _) => throw new InvalidOperationException("password=hunter2"), TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.IsType<UnexpectedError>(Assert.Single(result.Errors));
        Assert.Equal(UnexpectedError.UnexpectedCode, error.Code);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
        Assert.Empty(error.Reasons);
    }

    [Fact]
    public async Task Handle_when_the_handler_throws_logs_the_exception_under_the_trace_id_it_returns()
    {
        // Arrange
        var logger = new FakeLogger<ExceptionBehaviour<TestQuery, Result<int>>>();
        var behaviour = CreateBehaviour(logger);
        var boom = new InvalidOperationException("boom");
        using var activity = new Activity("request").Start();

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => throw boom, TestContext.Current.CancellationToken);

        // Assert
        var traceId = activity.TraceId.ToHexString();
        Assert.Equal(traceId, Assert.IsType<UnexpectedError>(Assert.Single(result.Errors)).TraceId);
        var logged = Assert.Single(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Error);
        Assert.Same(boom, logged.Exception);
        Assert.Contains(traceId, logged.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_when_a_translator_recognises_the_exception_returns_its_error_without_logging_it()
    {
        // Arrange
        var logger = new FakeLogger<ExceptionBehaviour<TestQuery, Result<int>>>();
        var behaviour = CreateBehaviour(logger, new TimeoutTranslator());

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => throw new TimeoutException(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Test.TimedOut", Assert.IsType<ConflictError>(Assert.Single(result.Errors)).Code);
        Assert.DoesNotContain(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Handle_when_no_translator_recognises_the_exception_returns_an_unexpected_error()
    {
        // Arrange
        var behaviour = CreateBehaviour(null, new TimeoutTranslator());

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => throw new InvalidOperationException(), TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<UnexpectedError>(Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Handle_when_the_handler_throws_returns_a_failed_result_for_a_non_generic_result_response()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestCommand, Result>([], new FakeLogger<ExceptionBehaviour<TestCommand, Result>>());

        // Act
        var result = await behaviour.Handle(new TestCommand(), (_, _) => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<UnexpectedError>(Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Handle_when_the_handler_is_cancelled_rethrows_instead_of_converting()
    {
        // Arrange
        var behaviour = CreateBehaviour();

        // Act
        var act = async () => await behaviour.Handle(new TestQuery(), (_, _) => throw new OperationCanceledException(), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task Handle_when_the_handler_succeeds_passes_the_result_through_untouched()
    {
        // Arrange
        var behaviour = CreateBehaviour();
        var expected = Result.Ok(42);

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(expected), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
    }
}

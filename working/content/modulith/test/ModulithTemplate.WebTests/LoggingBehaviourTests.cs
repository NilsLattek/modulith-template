using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using ModulithTemplate.Web.Behaviours;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="LoggingBehaviour{TMessage, TResponse}"/>.</summary>
public class LoggingBehaviourTests
{
    /// <summary>Stand-in message type; the behaviour never inspects the message itself.</summary>
    public sealed record TestQuery : IQuery<Result<int>>;

    [Fact]
    public async Task Handle_when_the_handler_succeeds_logs_the_outcome_at_information()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Ok(1)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Information);
        Assert.DoesNotContain(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Handle_when_the_handler_returns_a_failed_result_logs_the_outcome_at_warning()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Fail<int>("nope")), TestContext.Current.CancellationToken);

        // Assert
        var warning = Assert.Single(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
        Assert.Contains("nope", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_when_the_handler_succeeds_passes_the_result_through_untouched()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);
        var expected = Result.Ok(7);

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(expected), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
    }
}

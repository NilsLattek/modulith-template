using System.Diagnostics;

using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using ModulithTemplate.SharedKernel.Application.Behaviours;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="LoggingBehaviour{TMessage, TResponse}"/>.</summary>
public class LoggingBehaviourTests
{
    /// <summary>Stand-in message type; the behaviour never inspects the message itself.</summary>
    public sealed record TestQuery : IQuery<Result<int>>;

    /// <summary>
    /// Subscribes to the behaviour's activity source and collects every span it completes.
    /// </summary>
    /// <remarks>
    /// Without a listener that samples, <c>StartActivity</c> returns <see langword="null"/> and the
    /// behaviour records nothing — which is exactly what the tests not using this helper exercise.
    /// </remarks>
    /// <param name="recorded">The list each stopped activity is appended to.</param>
    /// <returns>The listener; dispose it to unsubscribe.</returns>
    private static ActivityListener ListenForSpans(List<Activity> recorded)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, "ModulithTemplate.Mediator", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = recorded.Add,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

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

    [Fact]
    public async Task Handle_when_the_handler_succeeds_records_a_span_named_after_the_message()
    {
        // Arrange
        var recorded = new List<Activity>();
        using var listener = ListenForSpans(recorded);
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>());

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Ok(1)), TestContext.Current.CancellationToken);

        // Assert
        var span = Assert.Single(recorded);
        Assert.Equal(nameof(TestQuery), span.DisplayName, StringComparer.Ordinal);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(typeof(TestQuery).FullName, span.GetTagItem("mediator.message.type"));
    }

    [Fact]
    public async Task Handle_when_the_handler_returns_a_failed_result_marks_the_span_as_failed()
    {
        // Arrange
        var recorded = new List<Activity>();
        using var listener = ListenForSpans(recorded);
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>());

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Fail<int>("nope")), TestContext.Current.CancellationToken);

        // Assert
        var span = Assert.Single(recorded);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("nope", span.StatusDescription, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Handle_when_the_handler_throws_ends_the_span_without_a_status()
    {
        // Arrange
        var recorded = new List<Activity>();
        using var listener = ListenForSpans(recorded);
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>());

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await behaviour.Handle(
                new TestQuery(),
                (_, _) => throw new OperationCanceledException(),
                TestContext.Current.CancellationToken));

        // Assert
        var span = Assert.Single(recorded);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }
}

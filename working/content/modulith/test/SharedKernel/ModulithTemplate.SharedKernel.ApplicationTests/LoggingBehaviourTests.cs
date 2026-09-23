using System.Diagnostics;

using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using ModulithTemplate.SharedKernel.Application.Behaviours;
using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.SharedKernel.ApplicationTests;

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
    public async Task Handle_when_the_handler_returns_an_unexpected_error_logs_its_code_at_warning()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);

        // Act
        await behaviour.Handle(
            new TestQuery(),
            (_, _) => ValueTask.FromResult(Result.Fail<int>(new UnexpectedError("trace"))),
            TestContext.Current.CancellationToken);

        // Assert
        var warning = Assert.Single(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
        Assert.Contains(UnexpectedError.UnexpectedCode, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_when_the_handler_rejects_the_message_logs_codes_at_information_and_never_messages()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);
        var rejection = new ValidationError("Name", "Test.NameTaken", "You entered 'alice@example.com'");

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Fail<int>(rejection)), TestContext.Current.CancellationToken);

        // Assert
        var records = logger.Collector.GetSnapshot();
        Assert.DoesNotContain(records, record => record.Level >= LogLevel.Warning);
        Assert.Contains(records, record => record.Message.Contains("Test.NameTaken", StringComparison.Ordinal));
        Assert.DoesNotContain(records, record => record.Message.Contains("alice@example.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_when_a_failure_is_outside_the_taxonomy_logs_its_type_name_not_its_message()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Fail<int>("secret detail")), TestContext.Current.CancellationToken);

        // Assert
        var warning = Assert.Single(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
        Assert.Contains(nameof(Error), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret detail", warning.Message, StringComparison.Ordinal);
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
    public async Task Handle_when_the_handler_returns_an_unexpected_error_marks_the_span_as_failed()
    {
        // Arrange
        var recorded = new List<Activity>();
        using var listener = ListenForSpans(recorded);
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>());

        // Act
        await behaviour.Handle(
            new TestQuery(),
            (_, _) => ValueTask.FromResult(Result.Fail<int>(new UnexpectedError(null))),
            TestContext.Current.CancellationToken);

        // Assert
        var span = Assert.Single(recorded);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(UnexpectedError.UnexpectedCode, span.StatusDescription, StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ExpectedErrors))]
    public async Task Handle_when_the_handler_rejects_the_message_leaves_the_span_status_unset(AppError rejection)
    {
        // Arrange
        var recorded = new List<Activity>();
        using var listener = ListenForSpans(recorded);
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>());

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Fail<int>(rejection)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityStatusCode.Unset, Assert.Single(recorded).Status);
    }

    public static TheoryData<AppError> ExpectedErrors() =>
    [
        new ValidationError("Name", "Test.Required", "required"),
        new NotFoundError("Test.Missing", "missing"),
        new ConflictError("Test.Shipped", "shipped"),
    ];

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

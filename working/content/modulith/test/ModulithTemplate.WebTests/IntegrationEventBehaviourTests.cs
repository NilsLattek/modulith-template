using FluentResults;

using FluentValidation;

using Mediator;

using Microsoft.Extensions.Logging.Abstractions;

using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.Web.Behaviours;
using ModulithTemplate.SharedKernel.Web.Errors;

using NSubstitute;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="IntegrationEventBehaviour{TMessage, TResponse}"/>.</summary>
public class IntegrationEventBehaviourTests
{
    /// <summary>Stand-in message type; the behaviour never inspects the message itself.</summary>
    public sealed record TestCommand(string Name) : ICommand<Result>;

    /// <summary>Stand-in message type for the generic <see cref="Result{TValue}"/> case.</summary>
    public sealed record TestQuery : IQuery<Result<int>>;

    private sealed class NameNotEmptyValidator : AbstractValidator<TestCommand>
    {
        public NameNotEmptyValidator() => RuleFor(command => command.Name).NotEmpty().WithMessage("name is required");
    }

    /// <summary>
    /// A queue whose flush throws, standing in for a consumer that threw. Hand-written rather than
    /// substituted: configuring a substitute to throw from a <see cref="ValueTask"/>-returning
    /// method means constructing a <see cref="ValueTask"/> only to discard it, which CA2012 rejects.
    /// </summary>
    /// <param name="toThrow">The exception the flush should throw.</param>
    private sealed class ThrowingQueue(Exception toThrow) : IIntegrationEventQueue
    {
        /// <summary>Whether <see cref="FlushAsync"/> was reached.</summary>
        public bool Flushed { get; private set; }

        /// <inheritdoc />
        public void Enqueue(IIntegrationEvent integrationEvent)
        {
        }

        /// <inheritdoc />
        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            Flushed = true;
            throw toThrow;
        }
    }

    [Fact]
    public async Task Handle_when_the_handler_succeeds_flushes_the_queue()
    {
        // Arrange
        var queue = Substitute.For<IIntegrationEventQueue>();
        var behaviour = new IntegrationEventBehaviour<TestCommand, Result>(queue);

        // Act
        var result = await behaviour.Handle(
            new TestCommand("a name"),
            (_, _) => ValueTask.FromResult(Result.Ok()),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        await queue.Received(1).FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_when_the_handler_fails_does_not_flush_the_queue()
    {
        // Arrange
        var queue = Substitute.For<IIntegrationEventQueue>();
        var behaviour = new IntegrationEventBehaviour<TestCommand, Result>(queue);

        // Act
        var result = await behaviour.Handle(
            new TestCommand("a name"),
            (_, _) => ValueTask.FromResult(Result.Fail("the handler rejected this")),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailed);
        await queue.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_for_a_generic_result_response_flushes_only_on_success()
    {
        // Arrange
        var queue = Substitute.For<IIntegrationEventQueue>();
        var behaviour = new IntegrationEventBehaviour<TestQuery, Result<int>>(queue);

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Ok(1)), TestContext.Current.CancellationToken);
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Fail<int>("no")), TestContext.Current.CancellationToken);

        // Assert
        await queue.Received(1).FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_when_validation_fails_upstream_never_reaches_the_flush()
    {
        // Arrange
        // Composed in the real pipeline order — ValidationBehaviour wraps IntegrationEventBehaviour,
        // so an invalid message must short-circuit before the inner behaviour runs at all.
        var queue = Substitute.For<IIntegrationEventQueue>();
        var inner = new IntegrationEventBehaviour<TestCommand, Result>(queue);
        var validation = new ValidationBehaviour<TestCommand, Result>([new NameNotEmptyValidator()]);
        var handlerCalled = false;

        // Act
        var result = await validation.Handle(
            new TestCommand(""),
            (message, token) => inner.Handle(message, (_, _) => { handlerCalled = true; return ValueTask.FromResult(Result.Ok()); }, token),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailed);
        Assert.False(handlerCalled);
        Assert.Single(result.Errors.OfType<ValidationError>());
        await queue.DidNotReceive().FlushAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_when_a_consumer_throws_surfaces_as_a_failed_result()
    {
        // Arrange
        // Documents the at-most-once trade rather than hiding it: the producer's write has already
        // committed by the time a consumer runs, so a throwing consumer cannot undo it — it can only
        // turn the caller's result red. ExceptionBehaviour is what performs that conversion.
        var boom = new InvalidOperationException("the consumer threw");
        var queue = new ThrowingQueue(boom);

        var inner = new IntegrationEventBehaviour<TestCommand, Result>(queue);
        var exception = new ExceptionBehaviour<TestCommand, Result>(
            NullLogger<ExceptionBehaviour<TestCommand, Result>>.Instance);
        var handlerCompleted = false;

        // Act
        var result = await exception.Handle(
            new TestCommand("a name"),
            (message, token) => inner.Handle(message, (_, _) => { handlerCompleted = true; return ValueTask.FromResult(Result.Ok()); }, token),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handlerCompleted);
        Assert.True(queue.Flushed);
        Assert.True(result.IsFailed);
        Assert.Same(boom, Assert.IsType<ExceptionalError>(Assert.Single(result.Errors)).Exception);
    }
}

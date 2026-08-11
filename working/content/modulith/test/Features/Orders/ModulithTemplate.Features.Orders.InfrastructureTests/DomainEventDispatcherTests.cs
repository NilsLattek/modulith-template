using Mediator;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

using ModulithTemplate.Domain.Common.Entities;
using ModulithTemplate.Domain.Common.Events;
using ModulithTemplate.Infrastructure.Common.Events;

namespace ModulithTemplate.Features.Orders.InfrastructureTests;

/// <summary>Tests for <see cref="DomainEventDispatcher"/>.</summary>
/// <remarks>
/// The dispatcher takes a delegate rather than a <c>DbContext</c> precisely so these guarantees can
/// be asserted without a database. <see cref="DomainEventDispatchInterceptor"/> is the thin adapter
/// that supplies the change tracker's aggregates to it.
/// </remarks>
public class DomainEventDispatcherTests
{
    private sealed record ThingHappenedDomainEvent(string What) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething(string what) => RaiseDomainEvent(new ThingHappenedDomainEvent(what));
    }

    /// <summary>
    /// Hand-written rather than substituted: configuring a substitute to run a callback from a
    /// <see cref="ValueTask"/>-returning method means building a <see cref="ValueTask"/> only to
    /// discard it, which CA2012 rejects.
    /// </summary>
    private sealed class RecordingPublisher : IPublisher
    {
        /// <summary>Every notification published, in order.</summary>
        public List<object> Published { get; } = [];

        /// <summary>Runs on each publish, so a test can observe state mid-dispatch.</summary>
        public Action<object>? OnPublish { get; set; }

        /// <inheritdoc />
        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => PublishCore(notification);

        /// <inheritdoc />
        public ValueTask Publish(object notification, CancellationToken cancellationToken = default) =>
            PublishCore(notification);

        private ValueTask PublishCore(object notification)
        {
            Published.Add(notification);
            OnPublish?.Invoke(notification);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Builds a dispatcher with a discard logger, for the tests that assert dispatch alone.</summary>
    private static DomainEventDispatcher Dispatcher(IPublisher publisher) =>
        new(publisher, NullLogger<DomainEventDispatcher>.Instance);

    [Fact]
    public async Task DispatchAsync_publishes_every_buffered_event()
    {
        // Arrange
        var publisher = new RecordingPublisher();
        var dispatcher = Dispatcher(publisher);
        var aggregate = new TestAggregate();
        aggregate.DoSomething("first");
        aggregate.DoSomething("second");

        // Act
        await dispatcher.DispatchAsync(() => [aggregate], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["first", "second"],
            publisher.Published.Cast<ThingHappenedDomainEvent>().Select(domainEvent => domainEvent.What),
            StringComparer.Ordinal);
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public async Task DispatchAsync_publishes_nothing_when_no_aggregate_raised_an_event()
    {
        // Arrange
        var publisher = new RecordingPublisher();
        var dispatcher = Dispatcher(publisher);

        // Act
        await dispatcher.DispatchAsync(() => [new TestAggregate()], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task DispatchAsync_clears_the_buffer_before_publishing()
    {
        // Arrange
        // The ordering that stops a handler which triggers a nested save from seeing — and
        // re-dispatching — the very events currently in flight.
        var publisher = new RecordingPublisher();
        var dispatcher = Dispatcher(publisher);
        var aggregate = new TestAggregate();
        aggregate.DoSomething("first");
        var bufferedDuringPublish = -1;
        publisher.OnPublish = _ => bufferedDuringPublish = aggregate.DomainEvents.Count;

        // Act
        await dispatcher.DispatchAsync(() => [aggregate], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, bufferedDuringPublish);
    }

    [Fact]
    public async Task DispatchAsync_dispatches_an_event_raised_by_a_handler()
    {
        // Arrange
        // A handler reacting to "first" raises "second" on the same aggregate; the dispatcher has to
        // re-read the change tracker to notice, rather than working from its first snapshot.
        var publisher = new RecordingPublisher();
        var dispatcher = Dispatcher(publisher);
        var aggregate = new TestAggregate();
        aggregate.DoSomething("first");
        publisher.OnPublish = published =>
        {
            if (published is ThingHappenedDomainEvent { What: "first" })
            {
                aggregate.DoSomething("second");
            }
        };

        // Act
        await dispatcher.DispatchAsync(() => [aggregate], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["first", "second"],
            publisher.Published.Cast<ThingHappenedDomainEvent>().Select(domainEvent => domainEvent.What),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task DispatchAsync_dispatches_an_aggregate_a_handler_added_later()
    {
        // Arrange
        var publisher = new RecordingPublisher();
        var dispatcher = Dispatcher(publisher);
        var first = new TestAggregate();
        first.DoSomething("first");
        var tracked = new List<AggregateRoot> { first };
        publisher.OnPublish = published =>
        {
            if (published is ThingHappenedDomainEvent { What: "first" })
            {
                var added = new TestAggregate();
                added.DoSomething("from-a-new-aggregate");
                tracked.Add(added);
            }
        };

        // Act
        await dispatcher.DispatchAsync(() => tracked, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["first", "from-a-new-aggregate"],
            publisher.Published.Cast<ThingHappenedDomainEvent>().Select(domainEvent => domainEvent.What),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task DispatchAsync_when_handlers_raise_events_forever_throws_instead_of_hanging()
    {
        // Arrange
        var publisher = new RecordingPublisher();
        var dispatcher = Dispatcher(publisher);
        var aggregate = new TestAggregate();
        aggregate.DoSomething("first");
        publisher.OnPublish = _ => aggregate.DoSomething("again");

        // Act
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await dispatcher.DispatchAsync(() => [aggregate], TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("did not settle", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchAsync_rejects_a_null_collector()
    {
        // Arrange
        var dispatcher = Dispatcher(new RecordingPublisher());

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await dispatcher.DispatchAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchAsync_logs_each_dispatched_event_at_debug()
    {
        // Arrange
        // Domain events are published, not sent, so no pipeline behaviour logs them; the dispatcher
        // is the only place a dispatch is recorded.
        var logger = new FakeLogger<DomainEventDispatcher>();
        var dispatcher = new DomainEventDispatcher(new RecordingPublisher(), logger);
        var aggregate = new TestAggregate();
        aggregate.DoSomething("first");

        // Act
        await dispatcher.DispatchAsync(() => [aggregate], TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Debug, record.Level);
        Assert.Contains(nameof(ThingHappenedDomainEvent), record.Message, StringComparison.Ordinal);
    }
}

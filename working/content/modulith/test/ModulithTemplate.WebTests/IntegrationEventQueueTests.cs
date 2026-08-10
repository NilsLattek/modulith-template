using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Application.Common.Events;
using ModulithTemplate.Features.Orders.Contracts.IntegrationEvents;
using ModulithTemplate.Infrastructure.Common.Events;

namespace ModulithTemplate.WebTests;

/// <summary>
/// Tests for the in-process integration event queue, exercised through the host's real mediator
/// rather than a substituted publisher.
/// </summary>
/// <remarks>
/// A substituted <see cref="IPublisher"/> would let these tests pass even if dispatch reached
/// nobody, which is the failure this design is most exposed to. The mediator's generated
/// <c>Publish</c> is a switch over the notification types present in the <b>host's</b> compilation:
/// an event type it has never seen falls through to a branch that neither dispatches nor throws, so
/// the event is silently dropped. Handlers, by contrast, are resolved from the container at publish
/// time — which is why a handler declared here can be registered by hand and still run.
/// <para>
/// These tests therefore use the real <see cref="SomeEntityAddedIntegrationEvent"/>: a probe event
/// declared in this assembly would be invisible to the host's generator and would silently do
/// nothing, proving only that the assertion was weak.
/// </para>
/// </remarks>
public class IntegrationEventQueueTests
{
    /// <summary>Payload prefix asking the probe handler to enqueue one follow-up event.</summary>
    private const string CascadePrefix = "cascade:";

    /// <summary>Payload asking the probe handler to enqueue an identical event, forever.</summary>
    private const string LoopPayload = "loop";

    /// <summary>Collects what the probe handler observed, shared across a provider as a singleton.</summary>
    public sealed class Recorder
    {
        /// <summary>Payloads handled, in dispatch order.</summary>
        public IList<string> Handled { get; } = new List<string>();
    }

    /// <summary>
    /// Stands in for a consuming feature's handler. Implements the derived
    /// <see cref="IIntegrationEventHandler{TEvent}"/>, as a real consumer does.
    /// </summary>
    /// <param name="recorder">Records that the handler ran.</param>
    /// <param name="queue">The scope's queue, so the handler can enqueue events of its own.</param>
    public sealed class ProbeHandler(Recorder recorder, IIntegrationEventQueue queue)
        : IIntegrationEventHandler<SomeEntityAddedIntegrationEvent>
    {
        /// <inheritdoc />
        public ValueTask Handle(SomeEntityAddedIntegrationEvent notification, CancellationToken cancellationToken)
        {
            recorder.Handled.Add(notification.Name);

            if (notification.Name.StartsWith(CascadePrefix, StringComparison.Ordinal))
            {
                queue.Enqueue(new SomeEntityAddedIntegrationEvent(notification.Name[CascadePrefix.Length..]));
            }
            else if (string.Equals(notification.Name, LoopPayload, StringComparison.Ordinal))
            {
                queue.Enqueue(new SomeEntityAddedIntegrationEvent(LoopPayload));
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Builds a provider wired the way the host is, plus the probe handler.
    /// </summary>
    /// <remarks>
    /// The handler is registered against <see cref="INotificationHandler{TNotification}"/> by hand
    /// because it lives in a test assembly. In the real host the source generator discovers and
    /// registers handlers automatically; what it cannot do is see a type that is not in its
    /// compilation.
    /// </remarks>
    private static ServiceProvider BuildProvider(Recorder recorder)
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        // The host registers an open generic IntegrationEventLogHandler<TEvent> for every event, and
        // it takes an ILogger — so a container without logging cannot activate any handler at all.
        services.AddLogging();
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddIntegrationEvents();
        services.AddScoped<INotificationHandler<SomeEntityAddedIntegrationEvent>, ProbeHandler>();
        return services.BuildServiceProvider();
    }

    private static IIntegrationEventQueue QueueIn(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IIntegrationEventQueue>();

    [Fact]
    public async Task FlushAsync_publishes_a_queued_event_to_its_handler()
    {
        // Arrange
        var recorder = new Recorder();
        await using var provider = BuildProvider(recorder);
        using var scope = provider.CreateScope();
        var queue = QueueIn(scope);
        queue.Enqueue(new SomeEntityAddedIntegrationEvent("first"));

        // Act
        await queue.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["first"], recorder.Handled);
    }

    [Fact]
    public async Task FlushAsync_publishes_queued_events_in_the_order_they_were_enqueued()
    {
        // Arrange
        var recorder = new Recorder();
        await using var provider = BuildProvider(recorder);
        using var scope = provider.CreateScope();
        var queue = QueueIn(scope);
        queue.Enqueue(new SomeEntityAddedIntegrationEvent("first"));
        queue.Enqueue(new SomeEntityAddedIntegrationEvent("second"));

        // Act
        await queue.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["first", "second"], recorder.Handled);
    }

    [Fact]
    public async Task FlushAsync_drains_an_event_enqueued_by_a_handler_in_the_same_flush()
    {
        // Arrange
        var recorder = new Recorder();
        await using var provider = BuildProvider(recorder);
        using var scope = provider.CreateScope();
        var queue = QueueIn(scope);
        queue.Enqueue(new SomeEntityAddedIntegrationEvent(CascadePrefix + "second"));

        // Act
        await queue.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([CascadePrefix + "second", "second"], recorder.Handled);
    }

    [Fact]
    public async Task FlushAsync_empties_the_queue_so_a_second_flush_republishes_nothing()
    {
        // Arrange
        var recorder = new Recorder();
        await using var provider = BuildProvider(recorder);
        using var scope = provider.CreateScope();
        var queue = QueueIn(scope);
        queue.Enqueue(new SomeEntityAddedIntegrationEvent("first"));

        // Act
        await queue.FlushAsync(TestContext.Current.CancellationToken);
        await queue.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["first"], recorder.Handled);
    }

    [Fact]
    public async Task FlushAsync_when_handlers_publish_in_a_cycle_throws_instead_of_hanging()
    {
        // Arrange
        var recorder = new Recorder();
        await using var provider = BuildProvider(recorder);
        using var scope = provider.CreateScope();
        var queue = QueueIn(scope);
        queue.Enqueue(new SomeEntityAddedIntegrationEvent(LoopPayload));

        // Act
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await queue.FlushAsync(TestContext.Current.CancellationToken));

        // Assert
        // Asserted on the drain-bound wording specifically: the event's type name alone also appears
        // in unrelated container-activation failures, which would let this pass for the wrong reason.
        Assert.Contains("exceeded", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SomeEntityAddedIntegrationEvent), thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enqueue_buffers_per_scope_so_one_scope_does_not_flush_another_scopes_events()
    {
        // Arrange
        var recorder = new Recorder();
        await using var provider = BuildProvider(recorder);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        QueueIn(first).Enqueue(new SomeEntityAddedIntegrationEvent("first"));

        // Act
        await QueueIn(second).FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(recorder.Handled);
    }

    [Fact]
    public void Enqueue_rejects_a_null_event()
    {
        // Arrange
        var recorder = new Recorder();
        using var provider = BuildProvider(recorder);
        using var scope = provider.CreateScope();
        var queue = QueueIn(scope);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }
}

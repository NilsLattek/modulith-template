using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ModulithTemplate.SharedKernel.Domain.Entities;
using ModulithTemplate.SharedKernel.Domain.Events;
using ModulithTemplate.SharedKernel.Infrastructure;
using ModulithTemplate.SharedKernel.Infrastructure.Events;

namespace ModulithTemplate.Features.Orders.InfrastructureTests;

/// <summary>Tests for <see cref="DomainEventDispatchInterceptor{TContext}"/>.</summary>
/// <remarks>
/// Covers only what the interceptor owns — which saves start a dispatch and which are suppressed as
/// re-entrant; the rounds themselves are <see cref="DomainEventDispatcherTests"/>. Nothing connects
/// to a database: the interceptor is invoked directly, as EF Core invokes it.
/// </remarks>
public class DomainEventDispatchInterceptorTests
{
    private const string ConnectionString = "Host=localhost;Database=modulith_tests";

    private sealed record ThingHappenedDomainEvent(string What) : IDomainEvent;

    private sealed class TrackedAggregate : AggregateRoot
    {
        public int Id { get; init; }

        public void DoSomething(string what) => RaiseDomainEvent(new ThingHappenedDomainEvent(what));
    }

    /// <summary>Stands in for one feature's context.</summary>
    private sealed class FirstFeatureContext(DbContextOptions<FirstFeatureContext> options) : DbContext(options)
    {
        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<TrackedAggregate>();
    }

    /// <summary>Stands in for another feature's context, registered in the same container.</summary>
    private sealed class SecondFeatureContext(DbContextOptions<SecondFeatureContext> options) : DbContext(options)
    {
        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<TrackedAggregate>();
    }

    /// <summary>
    /// Hand-written rather than substituted, for the reason given in
    /// <see cref="DomainEventDispatcherTests"/>, and with an asynchronous callback so a test can save
    /// a second context from "inside" a handler.
    /// </summary>
    private sealed class CallbackPublisher : IPublisher
    {
        /// <summary>Every notification published, in order.</summary>
        public List<object> Published { get; } = [];

        /// <summary>Runs on each publish, standing in for a domain event handler.</summary>
        public Func<object, ValueTask>? OnPublish { get; set; }

        /// <inheritdoc />
        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => PublishCore(notification);

        /// <inheritdoc />
        public ValueTask Publish(object notification, CancellationToken cancellationToken = default) =>
            PublishCore(notification);

        private ValueTask PublishCore(object notification)
        {
            Published.Add(notification);
            return OnPublish?.Invoke(notification) ?? ValueTask.CompletedTask;
        }
    }

    /// <summary>Builds the event data EF Core hands the interceptor at the start of a save.</summary>
    private static DbContextEventData SavingChanges(DbContext context) =>
        new(null!, static (_, _) => string.Empty, context);

    /// <summary>Adds a tracked aggregate that has already raised <paramref name="what"/>.</summary>
    private static TrackedAggregate Raised(DbContext context, int id, string what)
    {
        var aggregate = new TrackedAggregate { Id = id };
        context.Attach(aggregate);
        aggregate.DoSomething(what);
        return aggregate;
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:PostgresConnection"] = ConnectionString,
        })
        .Build();

    private static string[] Dispatched(CallbackPublisher publisher) =>
        [.. publisher.Published.Cast<ThingHappenedDomainEvent>().Select(domainEvent => domainEvent.What)];

    private static DomainEventDispatchInterceptor<FirstFeatureContext> Interceptor(IPublisher publisher) =>
        new(new DomainEventDispatcher(publisher, NullLogger<DomainEventDispatcher>.Instance));

    private static FirstFeatureContext Context() =>
        new(new DbContextOptionsBuilder<FirstFeatureContext>().UseNpgsql(ConnectionString).Options);

    [Fact]
    public async Task SavingChangesAsync_dispatches_another_feature_context_saved_from_a_handler()
    {
        // Arrange — the regression: a single shared interceptor would read a handler saving feature
        // two's context as re-entrancy and drop its events silently.
        var publisher = new CallbackPublisher();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPublisher>(publisher);
        services.AddModuleDbContext<FirstFeatureContext>(Configuration(), schema: "first");
        services.AddModuleDbContext<SecondFeatureContext>(Configuration(), schema: "second");

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<FirstFeatureContext>();
        var second = scope.ServiceProvider.GetRequiredService<SecondFeatureContext>();
        var secondInterceptor =
            scope.ServiceProvider.GetRequiredService<DomainEventDispatchInterceptor<SecondFeatureContext>>();

        Raised(first, id: 1, what: "first-feature");
        var secondAggregate = Raised(second, id: 2, what: "second-feature");
        publisher.OnPublish = async published =>
        {
            if (published is ThingHappenedDomainEvent { What: "first-feature" })
            {
                await secondInterceptor.SavingChangesAsync(
                    SavingChanges(second), default, TestContext.Current.CancellationToken);
            }
        };

        // Act
        await scope.ServiceProvider.GetRequiredService<DomainEventDispatchInterceptor<FirstFeatureContext>>()
            .SavingChangesAsync(SavingChanges(first), default, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["first-feature", "second-feature"], Dispatched(publisher), StringComparer.Ordinal);
        Assert.Empty(secondAggregate.DomainEvents);
    }

    [Fact]
    public async Task SavingChangesAsync_suppresses_a_nested_save_on_the_context_being_dispatched()
    {
        // Arrange
        // A handler that saves the very context under dispatch must not start a competing dispatch:
        // the outer loop re-reads the change tracker each round and picks the new event up itself.
        var publisher = new CallbackPublisher();
        var interceptor = Interceptor(publisher);
        using var context = Context();
        var aggregate = Raised(context, id: 1, what: "first");
        var publishedDuringNestedSave = -1;
        publisher.OnPublish = async published =>
        {
            if (published is ThingHappenedDomainEvent { What: "first" })
            {
                aggregate.DoSomething("nested");
                await interceptor.SavingChangesAsync(
                    SavingChanges(context), default, TestContext.Current.CancellationToken);
                publishedDuringNestedSave = publisher.Published.Count;
            }
        };

        // Act
        await interceptor.SavingChangesAsync(
            SavingChanges(context), default, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, publishedDuringNestedSave);
        Assert.Equal(["first", "nested"], Dispatched(publisher), StringComparer.Ordinal);
    }

    [Fact]
    public async Task SavingChangesAsync_dispatches_again_on_a_later_save_of_the_same_context()
    {
        // Arrange
        // The guard is released when dispatch finishes, so it suppresses nested saves only.
        var publisher = new CallbackPublisher();
        var interceptor = Interceptor(publisher);
        using var context = Context();
        var aggregate = Raised(context, id: 1, what: "first");

        // Act
        await interceptor.SavingChangesAsync(
            SavingChanges(context), default, TestContext.Current.CancellationToken);
        aggregate.DoSomething("second");
        await interceptor.SavingChangesAsync(
            SavingChanges(context), default, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["first", "second"], Dispatched(publisher), StringComparer.Ordinal);
    }

    [Fact]
    public async Task SavingChangesAsync_ignores_a_save_of_another_context_type()
    {
        // Arrange
        // The interceptor is only ever attached to its own context, but the type check is what makes
        // the instance-level guard safe — it can never be flipped by a foreign context.
        var publisher = new CallbackPublisher();
        var interceptor = Interceptor(publisher);
        var options = new DbContextOptionsBuilder<SecondFeatureContext>().UseNpgsql(ConnectionString).Options;
        using var foreign = new SecondFeatureContext(options);
        Raised(foreign, id: 1, what: "elsewhere");

        // Act
        await interceptor.SavingChangesAsync(
            SavingChanges(foreign), default, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(publisher.Published);
    }
}

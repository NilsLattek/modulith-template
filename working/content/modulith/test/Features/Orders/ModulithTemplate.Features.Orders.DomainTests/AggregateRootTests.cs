using ModulithTemplate.Domain.Common.Entities;
using ModulithTemplate.Domain.Common.Events;

namespace ModulithTemplate.Features.Orders.DomainTests;

/// <summary>Tests for <see cref="AggregateRoot"/>.</summary>
public class AggregateRootTests
{
    private sealed record ThingHappenedDomainEvent(string What) : IDomainEvent;

    /// <summary>
    /// A minimal aggregate. <c>RaiseDomainEvent</c> is protected, so the only way to buffer an event
    /// is through a named method that says what happened — which is the rule being demonstrated.
    /// </summary>
    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething(string what) => RaiseDomainEvent(new ThingHappenedDomainEvent(what));
    }

    [Fact]
    public void DomainEvents_on_a_new_aggregate_is_empty()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act / Assert
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void RaiseDomainEvent_buffers_the_event_in_order()
    {
        // Arrange
        var aggregate = new TestAggregate();

        // Act
        aggregate.DoSomething("first");
        aggregate.DoSomething("second");

        // Assert
        Assert.Equal(
            ["first", "second"],
            aggregate.DomainEvents.Cast<ThingHappenedDomainEvent>().Select(domainEvent => domainEvent.What),
            StringComparer.Ordinal);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_buffer()
    {
        // Arrange
        var aggregate = new TestAggregate();
        aggregate.DoSomething("first");

        // Act
        aggregate.ClearDomainEvents();

        // Assert
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void DomainEvents_is_a_read_only_view_not_the_backing_list()
    {
        // Arrange
        var aggregate = new TestAggregate();
        aggregate.DoSomething("first");

        // Act
        var events = aggregate.DomainEvents;

        // Assert
        // Returning the private list directly would let a caller cast back to List<IDomainEvent> and
        // add an event that no mutator method ever authorised.
        Assert.IsNotType<List<IDomainEvent>>(events);
    }
}

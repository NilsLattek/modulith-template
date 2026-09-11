using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Application.DomainEventHandlers;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Orders.Domain.Events;
using ModulithTemplate.SharedKernel.Application.Events;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Tests for <see cref="SomeEntityAddedDomainEventHandler"/>.</summary>
/// <remarks>
/// The translation is what this handler exists for, so the assertions are about the contract it
/// produces, not about the publisher having been called.
/// </remarks>
public class SomeEntityAddedDomainEventHandlerTests
{
    private static SomeEntityAddedIntegrationEvent Translate(SomeEntityAddedDomainEvent domainEvent)
    {
        var publisher = Substitute.For<IOrdersIntegrationEventPublisher>();
        var published = new List<IIntegrationEvent>();
        publisher.When(p => p.Publish(Arg.Any<IIntegrationEvent>()))
            .Do(call => published.Add(call.Arg<IIntegrationEvent>()));

        new SomeEntityAddedDomainEventHandler(publisher)
            .Handle(domainEvent, TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return Assert.IsType<SomeEntityAddedIntegrationEvent>(Assert.Single(published));
    }

    [Fact]
    public void Handle_publishes_the_domain_events_data_in_the_published_language()
    {
        // Arrange
        var someEntityId = Guid.CreateVersion7();

        // Act
        var integrationEvent = Translate(new SomeEntityAddedDomainEvent(someEntityId, "a name", 12.34m));

        // Assert
        Assert.Equal(someEntityId, integrationEvent.SomeEntityId);
        Assert.Equal("a name", integrationEvent.Name, StringComparer.Ordinal);
        Assert.Equal(12.34m, integrationEvent.Amount);
    }

    [Fact]
    public void Handle_groups_the_event_by_the_aggregate_it_concerns()
    {
        // Arrange
        var someEntityId = Guid.CreateVersion7();

        // Act
        var integrationEvent = Translate(new SomeEntityAddedDomainEvent(someEntityId, "a name", 1m));

        // Assert
        // Events about one entity are delivered in order; events about different ones proceed
        // concurrently. A shared constant here would serialise the whole application.
        Assert.Equal(someEntityId.ToString(), integrationEvent.GroupKey, StringComparer.Ordinal);
    }

    [Fact]
    public void Handle_gives_each_published_event_its_own_identity()
    {
        // Arrange
        var domainEvent = new SomeEntityAddedDomainEvent(Guid.CreateVersion7(), "a name", 1m);

        // Act
        var first = Translate(domainEvent);
        var second = Translate(domainEvent);

        // Assert
        Assert.NotEqual(Guid.Empty, first.EventId);
        Assert.NotEqual(first.EventId, second.EventId);
    }
}

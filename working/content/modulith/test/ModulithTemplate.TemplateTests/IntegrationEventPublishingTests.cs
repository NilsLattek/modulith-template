using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// Who an event is staged for is decided by the handlers declared: an inbox row per Consumer, an outbox
/// row when something forwards it outside the process, and a refusal when neither is declared.
/// </summary>
public class IntegrationEventPublishingTests
{
    [Fact]
    public async Task An_event_nothing_receives_is_refused_at_publish()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        using var scope = solution.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IOrdersIntegrationEventPublisher>();

        // Act
        var act = () => publisher.Publish(new UnheardOfIntegrationEvent(Guid.CreateVersion7(), "a group"));

        // Assert
        // Loud at once, rather than a forgotten Consumer found later in production data.
        var error = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains(nameof(UnheardOfIntegrationEvent), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_forwarded_event_is_staged_in_the_outbox_beside_its_consumers_rows()
    {
        // Arrange
        // What the outbox source generator registers for a declared IOutboxMessageHandler<T>; the
        // handler is never run here, so any type stands in for it.
        await using var solution = SolutionUnderTest.Start(app => app.Services.AddSingleton(
            new HandlerEntry<OutboxMessage>(
                typeof(SomeEntityAddedIntegrationEvent).FullName!,
                typeof(IntegrationEventPublishingTests),
                typeof(SomeEntityAddedIntegrationEvent),
                (_, _, _, _) => Task.CompletedTask)));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await solution.SendAsync(new AddSomeEntityCommand("a name", 12.34m), cancellationToken);

        // Assert
        // A broker integration sits beside the in-process Consumers rather than displacing them.
        var forwarded = Assert.Single(await solution.OutboxRowsAsync(cancellationToken));
        Assert.Equal(typeof(SomeEntityAddedIntegrationEvent).FullName, forwarded.Type, StringComparer.Ordinal);
        Assert.Single(await solution.InboxRowsAsync(cancellationToken));
    }

    /// <summary>An event declared without any Consumer or forwarder.</summary>
    public sealed record UnheardOfIntegrationEvent(Guid EventId, string GroupKey) : IIntegrationEvent;
}

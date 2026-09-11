using System.Text.Json;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;
using Underground.Outbox.Exceptions;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>
/// Turns a claimed outbox row back into its integration event and publishes it in-process, where
/// every consumer receives it through an ordinary mediator notification handler.
/// </summary>
/// <remarks>
/// Replaces the dispatcher the outbox source generator emits. That one routes by scanning for
/// handler <i>classes</i> naming a concrete message type, which no generic republisher can satisfy —
/// verified: an open generic handler makes it emit a type parameter it cannot resolve, and a handler
/// closing one by inheritance is not discovered at all. Routing through the registry instead keeps
/// one republisher for every event, so adding an integration event costs a record and a handler.
/// <para>
/// Each message arrives with a fresh <see cref="IServiceScope"/>, so consumers get their own
/// <c>DbContext</c> and commit in their own transaction — never the publisher's.
/// </para>
/// </remarks>
/// <param name="registry">Maps a row's stored type name back to the event type.</param>
public sealed class IntegrationEventRepublisher(IntegrationEventRegistry registry)
    : IMessageDispatcher<OutboxMessage>
{
    /// <inheritdoc />
    public async Task ExecuteAsync(
        IServiceScope scope, OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(registry);

        var eventType = registry.Find(message.Type)
            ?? throw new ParsingException(
                $"No integration event is registered as '{message.Type}' for message {message.Id}. "
                + "Register it with AddIntegrationEvent<T>() in its feature's Configure<Name>Application.");

        var integrationEvent = JsonSerializer.Deserialize(message.Data, eventType)
            ?? throw new ParsingException(
                $"Cannot parse event body {message.Data} of message: {message.Id}");

        // The object overload, deliberately: both Publish overloads switch on the runtime type, and
        // the generic one would key on IIntegrationEvent. A type the host's mediator generator never
        // saw falls into a default branch that neither dispatches nor throws — which is exactly what
        // the MSG0005 build failure for an event with no consumer exists to prevent.
        await scope.ServiceProvider
            .GetRequiredService<IPublisher>()
            .Publish(integrationEvent, cancellationToken);
    }
}

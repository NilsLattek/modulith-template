using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.Features.Orders.Contracts.IntegrationEvents;

namespace ModulithTemplate.Features.Orders.Application.IntegrationEventHandlers;

/// <summary>
/// Reacts to <see cref="SomeEntityAddedIntegrationEvent"/>.
/// </summary>
/// <remarks>
/// <para>
/// Normally a consumer lives in a <b>different</b> feature from the producer — integration events are
/// cross-feature contracts. This one sits in Orders only because the scaffold ships a single feature;
/// a real handler would react by writing through its own feature's <c>DbContext</c>, kept idempotent.
/// Delete it with the rest of the <c>SomeEntity</c> placeholder.
/// </para>
/// </remarks>
public sealed class SomeEntityAddedIntegrationEventHandler
    : IIntegrationEventHandler<SomeEntityAddedIntegrationEvent>
{
    /// <inheritdoc />
    public ValueTask Handle(SomeEntityAddedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        // A real consumer reacts here — update this feature's read model, enqueue further work, and
        // so on — through its own feature's repository. The placeholder does nothing.
        return ValueTask.CompletedTask;
    }
}

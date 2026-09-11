using System.Text.Json.Serialization;

using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.Features.Orders.Contracts.Events;

/// <summary>
/// Announces that Orders recorded a new entity. Published by Orders; consumable by any feature.
/// </summary>
/// <remarks>
/// Orders' published language, not its domain model: a consumer compiles against this record alone
/// and never sees <c>SomeEntity</c>. Its full type name is stored on every outbox row, so renaming
/// or moving it orphans the rows already written.
/// </remarks>
/// <param name="EventId">This event's identity, for a consumer that must deduplicate.</param>
/// <param name="SomeEntityId">The entity Orders recorded.</param>
/// <param name="Name">Its name.</param>
/// <param name="Amount">The amount it was recorded for.</param>
public sealed record SomeEntityAddedIntegrationEvent(
    Guid EventId,
    Guid SomeEntityId,
    string Name,
    decimal Amount) : IIntegrationEvent
{
    /// <inheritdoc />
    /// <remarks>
    /// The entity this event concerns, so two events about it are delivered in order while events
    /// about unrelated entities proceed concurrently. Derived rather than passed in, so no publish
    /// site can supply the wrong one; <see cref="JsonIgnoreAttribute"/> because it is recomputed on
    /// the way out rather than stored.
    /// </remarks>
    [JsonIgnore]
    public string GroupKey => SomeEntityId.ToString();
}

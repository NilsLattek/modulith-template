using ModulithTemplate.SharedKernel.Domain.Events;

namespace ModulithTemplate.Features.Orders.Domain.Events;

/// <summary>Raised when a <c>SomeEntity</c> has been created.</summary>
/// <remarks>
/// Internal to Orders: it speaks this feature's own language and no sibling ever sees it. An
/// application-layer handler translates it into the published integration event.
/// </remarks>
/// <param name="SomeEntityId">The new entity's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Amount">The amount it was created for.</param>
public sealed record SomeEntityAddedDomainEvent(Guid SomeEntityId, string Name, decimal Amount) : IDomainEvent;

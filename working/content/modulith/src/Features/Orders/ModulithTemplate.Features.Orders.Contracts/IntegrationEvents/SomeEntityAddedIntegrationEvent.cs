using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.Features.Orders.Contracts.IntegrationEvents;

/// <summary>
/// Published when the Orders feature has added a new entity.
/// </summary>
/// <remarks>
/// A worked example of the contract rules, and the shape to copy: a sealed record of primitives
/// only. No entity, no value object, no <c>Result</c> — a consumer compiled against this type must
/// not need anything out of the Orders domain to read it.
/// <para>
/// Being a published contract, it changes additively. Adding an optional property is safe; renaming
/// or removing one is not, and calls for a new <c>SomeEntityAddedV2IntegrationEvent</c> published
/// alongside this one until every consumer has moved.
/// </para>
/// </remarks>
/// <param name="Name">The name the entity was added under.</param>
public sealed record SomeEntityAddedIntegrationEvent(string Name) : IIntegrationEvent;


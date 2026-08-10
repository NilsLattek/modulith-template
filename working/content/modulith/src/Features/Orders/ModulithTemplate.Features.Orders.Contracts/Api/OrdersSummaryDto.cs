namespace ModulithTemplate.Features.Orders.Contracts.Api;

/// <summary>
/// What another feature is allowed to know about the state of Orders.
/// </summary>
/// <remarks>
/// A contract read model, not this feature's internal <c>Application/Dtos/</c> shape. The same
/// primitives-only rule as an integration event applies: exposing a domain entity here would hand
/// every consumer a dependency on the Orders domain, which is precisely what the Contracts project
/// exists to prevent.
/// </remarks>
/// <param name="EntityCount">How many entities the Orders feature currently holds.</param>
public sealed record OrdersSummaryDto(int EntityCount);

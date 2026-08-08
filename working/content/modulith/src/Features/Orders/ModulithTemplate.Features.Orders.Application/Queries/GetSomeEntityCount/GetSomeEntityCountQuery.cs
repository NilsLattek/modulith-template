using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;

/// <summary>
/// Counts the entities owned by the Orders feature. Replace this with a real query once the
/// feature has one — it exists to demonstrate the query shape, not to be useful. Throws against
/// a real database until <see cref="Domain.Entities.SomeEntity"/> is mapped in
/// <c>OrdersContext</c>, because the placeholder entity is deliberately unmapped.
/// </summary>
public sealed record GetSomeEntityCountQuery : IQuery<Result<int>>;

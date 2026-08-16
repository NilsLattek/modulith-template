using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;

/// <summary>
/// Demonstrates the query shape; replace it with a real query. Throws against a real database,
/// because <see cref="Domain.Entities.SomeEntity"/> is deliberately unmapped in <c>OrdersContext</c>.
/// </summary>
public sealed record GetSomeEntityCountQuery : IQuery<Result<int>>;

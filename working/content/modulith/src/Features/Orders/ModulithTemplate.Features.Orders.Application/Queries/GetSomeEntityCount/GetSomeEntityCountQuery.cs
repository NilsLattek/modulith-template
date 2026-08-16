using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;

/// <summary>
/// Demonstrates the query shape; replace it with a real query. Counts the rows behind
/// <see cref="Domain.Entities.SomeEntity"/>, so it needs the Orders migrations applied.
/// </summary>
public sealed record GetSomeEntityCountQuery : IQuery<Result<int>>;

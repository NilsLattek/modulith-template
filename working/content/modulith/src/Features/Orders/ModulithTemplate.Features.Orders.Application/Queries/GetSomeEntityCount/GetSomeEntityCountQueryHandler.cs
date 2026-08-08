using FluentResults;

using Mediator;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;

/// <summary>Handles <see cref="GetSomeEntityCountQuery"/>.</summary>
/// <param name="repository">The Orders feature's repository.</param>
public sealed class GetSomeEntityCountQueryHandler(IOrdersRepository<SomeEntity> repository)
    : IQueryHandler<GetSomeEntityCountQuery, Result<int>>
{
    /// <inheritdoc />
    public async ValueTask<Result<int>> Handle(GetSomeEntityCountQuery query, CancellationToken cancellationToken)
    {
        var count = await repository.CountAsync(cancellationToken);
        return Result.Ok(count);
    }
}

using ModulithTemplate.Features.Orders.Contracts.Api;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Application.Api;

/// <summary>
/// Implements <see cref="IOrdersApi"/> for callers in other features.
/// </summary>
/// <remarks>
/// The interface is published in <c>Contracts</c>; the implementation stays here in Application,
/// where it can reach the repository. A consumer therefore compiles against the contract alone and
/// never sees the Orders domain.
/// </remarks>
/// <param name="repository">The Orders feature's repository.</param>
public sealed class OrdersApi(IOrdersRepository<SomeEntity> repository) : IOrdersApi
{
    /// <inheritdoc />
    public async ValueTask<OrdersSummaryDto> GetSummaryAsync(CancellationToken cancellationToken) =>
        new(await repository.CountAsync(cancellationToken));
}

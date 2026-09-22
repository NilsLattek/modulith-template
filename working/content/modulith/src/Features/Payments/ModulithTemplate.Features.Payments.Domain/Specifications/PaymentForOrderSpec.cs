using Ardalis.Specification;

using ModulithTemplate.Features.Payments.Domain.Entities;

namespace ModulithTemplate.Features.Payments.Domain.Specifications;

/// <summary>Selects the payments already recorded against one order.</summary>
/// <remarks>
/// The criteria are added in the constructor, which is the only place <c>Query</c> is meant to be
/// used: shadowing it with a property leaves the specification with no criteria at all, so it
/// silently matches every row.
/// </remarks>
public sealed class PaymentForOrderSpec : Specification<Payment>
{
    /// <summary>Selects payments for one order.</summary>
    /// <param name="orderId">The order to look for.</param>
    public PaymentForOrderSpec(Guid orderId) => Query.Where(payment => payment.OrderId == orderId);
}

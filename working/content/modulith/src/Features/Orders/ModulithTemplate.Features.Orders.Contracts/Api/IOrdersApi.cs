namespace ModulithTemplate.Features.Orders.Contracts.Api;

/// <summary>
/// The Orders feature's synchronous, read-only API for other features.
/// </summary>
/// <remarks>
/// This is how one feature reaches another: "I need Orders data to finish handling the request I am
/// in right now".
/// </remarks>
public interface IOrdersApi
{
    /// <summary>
    /// Returns a summary of the Orders feature's current state.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The summary.</returns>
    ValueTask<OrdersSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}

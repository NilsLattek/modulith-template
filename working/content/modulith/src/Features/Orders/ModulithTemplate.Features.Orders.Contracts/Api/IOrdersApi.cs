namespace ModulithTemplate.Features.Orders.Contracts.Api;

/// <summary>
/// The Orders feature's synchronous, read-only API for other features.
/// </summary>
/// <remarks>
/// The counterpart to this feature's integration events, and the answer to a different question.
/// An event says "this happened, react whenever"; this interface answers "I need Orders data to
/// finish handling the request I am in right now". Forcing the second case through events is what
/// pushes features into duplicating each other's data.
/// <para>
/// <b>Read-only by design.</b> A method that changes Orders state must never appear here. A feature
/// that needs to command another feature is a sign the boundary is in the wrong place — the two are
/// one bounded context, or the interaction belongs in the caller's own domain. Reacting to an
/// integration event is how a feature legitimately causes work elsewhere.
/// </para>
/// <para>
/// Being part of the published contract, this interface changes additively: adding a method is safe,
/// changing an existing signature breaks every consumer compiled against it.
/// </para>
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

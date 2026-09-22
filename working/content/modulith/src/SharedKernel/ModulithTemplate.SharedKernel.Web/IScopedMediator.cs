using Mediator;

namespace ModulithTemplate.SharedKernel.Web;

/// <summary>
/// Sends a message inside a dependency injection scope of its own.
/// </summary>
/// <remarks>
/// Not the mediator's <c>Scoped</c> registration, which resolves from the <i>caller's</i> scope — in
/// Blazor Server the whole circuit, so every component would share one long-lived, non-thread-safe
/// <c>DbContext</c>. This opens a new scope per message and disposes it when the message completes.
/// <para>
/// A response therefore outlives the scope that produced it, so it must not depend on it: handlers
/// return DTOs, never entities. Web-only by construction — no Application or Infrastructure project
/// references this assembly, so a handler cannot nest a scope and split its own transaction.
/// </para>
/// </remarks>
public interface IScopedMediator
{
    /// <summary>Sends a request in a new scope.</summary>
    /// <typeparam name="TResponse">What the request returns.</typeparam>
    /// <param name="message">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The handler's response.</returns>
    /// <remarks>
    /// One overload per marker, because Mediator derives <see cref="IRequest{TResponse}"/>,
    /// <see cref="ICommand{TResponse}"/> and <see cref="IQuery{TResponse}"/> from
    /// <c>IMessage</c> as siblings — none is assignable to another.
    /// </remarks>
    ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> message, CancellationToken cancellationToken = default);

    /// <summary>Sends a command in a new scope.</summary>
    /// <typeparam name="TResponse">What the command returns.</typeparam>
    /// <param name="message">The command to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The handler's response.</returns>
    ValueTask<TResponse> Send<TResponse>(
        ICommand<TResponse> message, CancellationToken cancellationToken = default);

    /// <summary>Sends a query in a new scope.</summary>
    /// <typeparam name="TResponse">What the query returns.</typeparam>
    /// <param name="message">The query to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The handler's response.</returns>
    ValueTask<TResponse> Send<TResponse>(
        IQuery<TResponse> message, CancellationToken cancellationToken = default);
}

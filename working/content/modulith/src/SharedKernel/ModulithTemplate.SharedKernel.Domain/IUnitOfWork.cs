using System.Data;

namespace ModulithTemplate.SharedKernel.Domain;

/// <summary>
/// Coordinates several persistence operations inside a single database transaction.
/// </summary>
/// <remarks>
/// A single <c>SaveChangesAsync</c> is already atomic, and the domain events it raises are
/// dispatched inside it, so their handlers' writes commit with it. This abstraction is for the case
/// that is not covered by that: an operation whose steps span <i>more than one save</i> — a domain
/// service that persists between calls, a handler that writes, reads back, then writes again — and
/// which must still commit or roll back as a whole.
/// <para>
/// Features implement this the same way they implement <see cref="IRepository{T}"/>: through their
/// own derived interface, bound to their own <c>DbContext</c>. Handlers and domain services inject
/// that per-feature interface, never this one — a container binding <c>IUnitOfWork</c> itself would
/// keep only the last feature registered and hand every other feature the wrong context's
/// transaction.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="action"/> inside a transaction, committing when it returns and rolling
    /// back if it throws.
    /// </summary>
    /// <remarks>
    /// The exception is rethrown unchanged after the rollback: the host's exception behaviour turns
    /// it into a failed result, so callers branch on <c>IsFailed</c> rather than catching here.
    /// Calls must not be nested — the underlying context refuses to begin a transaction while one
    /// is already open.
    /// </remarks>
    /// <param name="action">The work to execute transactionally.</param>
    /// <param name="isolationLevel">
    /// The isolation level to begin the transaction at, or <see langword="null"/> for the
    /// database's default.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ExecuteInTransactionAsync(
        Func<Task> action,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a transaction and returns its result, committing when
    /// it returns and rolling back if it throws.
    /// </summary>
    /// <typeparam name="T">The type <paramref name="action"/> produces.</typeparam>
    /// <param name="action">The work to execute transactionally.</param>
    /// <param name="isolationLevel">
    /// The isolation level to begin the transaction at, or <see langword="null"/> for the
    /// database's default.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Whatever <paramref name="action"/> returned, once the transaction has committed.</returns>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default);
}

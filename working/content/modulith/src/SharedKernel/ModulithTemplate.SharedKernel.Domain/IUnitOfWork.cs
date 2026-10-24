using System.Data;

namespace ModulithTemplate.SharedKernel.Domain;

/// <summary>
/// Coordinates several persistence operations inside a single database transaction.
/// </summary>
/// <remarks>
/// A single <c>SaveChangesAsync</c> is already atomic, domain events included. This is for what
/// that does not cover: an operation spanning <i>more than one save</i> which must still commit or
/// roll back as a whole. Like <see cref="IRepository{T}"/>, features inject their own derived
/// interface rather than this one: a container binding <c>IUnitOfWork</c> itself would keep only
/// the last feature registered and hand every other one the wrong context's transaction.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="action"/> inside a transaction, committing when it returns and rolling
    /// back if it throws; a transaction already open is joined instead, and left to its owner.
    /// </summary>
    /// <remarks>
    /// A retrying execution strategy may run <paramref name="action"/> again from an empty change
    /// tracker, so it must be re-runnable: load and stage everything inside it.
    /// </remarks>
    /// <param name="action">The work to execute transactionally.</param>
    /// <param name="isolationLevel">
    /// The isolation level to begin the transaction at, or <see langword="null"/> for the
    /// database's default. Ignored when joining an open transaction.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ExecuteInTransactionAsync(
        Func<Task> action,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a transaction and returns its result, committing when
    /// it returns and rolling back if it throws; a transaction already open is joined instead.
    /// </summary>
    /// <remarks>
    /// <paramref name="action"/> must be re-runnable, for the reasons given on the overload without
    /// a result.
    /// </remarks>
    /// <typeparam name="T">The type <paramref name="action"/> produces.</typeparam>
    /// <param name="action">The work to execute transactionally.</param>
    /// <param name="isolationLevel">
    /// The isolation level to begin the transaction at, or <see langword="null"/> for the
    /// database's default. Ignored when joining an open transaction.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Whatever <paramref name="action"/> returned.</returns>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default);
}

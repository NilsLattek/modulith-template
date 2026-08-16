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
    /// back if it throws.
    /// </summary>
    /// <remarks>
    /// The exception is rethrown unchanged after the rollback, for the host's exception behaviour
    /// to turn into a failed result. Calls must not be nested — the context refuses to begin a
    /// transaction while one is already open.
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

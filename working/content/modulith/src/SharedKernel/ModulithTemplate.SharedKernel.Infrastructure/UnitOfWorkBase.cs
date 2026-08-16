using System.Data;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Domain;

namespace ModulithTemplate.SharedKernel.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/> for a feature's own context.
/// </summary>
/// <remarks>
/// The counterpart to <c>RepositoryBase&lt;T&gt;</c>, and derived from the same way: a feature
/// declares a body-less <c>XxxUnitOfWork : UnitOfWorkBase&lt;XxxContext&gt;, IXxxUnitOfWork</c>
/// purely to bind its marker interface to its own context, which is what keeps one feature's
/// transactions off another's connection. Generic in the context type rather than taking a plain
/// <c>DbContext</c>, so a sibling feature's context would not compile.
/// </remarks>
/// <typeparam name="TContext">The feature's context type.</typeparam>
/// <param name="dbContext">The feature's context, whose connection the transaction is opened on.</param>
public abstract class UnitOfWorkBase<TContext>(TContext dbContext) : IUnitOfWork
    where TContext : DbContext
{
    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<Task> action,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await ExecuteInTransactionAsync<object?>(
            async () =>
            {
                await action();
                return null;
            },
            isolationLevel,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await using var transaction = isolationLevel is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : await dbContext.Database.BeginTransactionAsync(isolationLevel.Value, cancellationToken);

        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            // Rolled back explicitly rather than left to disposal, so the transaction is finished
            // before the exception reaches the caller. CancellationToken.None on purpose: the usual
            // reason to land here is that cancellationToken was cancelled, and passing it on would
            // abandon the rollback and replace the original exception with a cancellation.
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

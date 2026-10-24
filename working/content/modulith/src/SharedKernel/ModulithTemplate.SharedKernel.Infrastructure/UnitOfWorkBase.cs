using System.Data;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Domain;

namespace ModulithTemplate.SharedKernel.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/> for a feature's own context.
/// </summary>
/// <remarks>
/// The counterpart to <c>FeatureRepository&lt;T&gt;</c>, and derived from the same way: a feature
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
        // Commit, rollback and retries belong to whoever opened the transaction.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var replay = false;

        return await strategy.ExecuteAsync(
            async ct =>
            {
                // A failed attempt's saves were accepted by the change tracker though their
                // transaction rolled back: left there, a replay would skip them as already made and
                // re-add whatever the failed save left behind.
                if (replay)
                {
                    dbContext.ChangeTracker.Clear();
                }

                replay = true;

                await using var transaction = isolationLevel is null
                    ? await dbContext.Database.BeginTransactionAsync(ct)
                    : await dbContext.Database.BeginTransactionAsync(isolationLevel.Value, ct);

                try
                {
                    var result = await action();
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    // Rolled back explicitly rather than left to disposal, so the transaction is finished
                    // before the exception reaches the caller. CancellationToken.None on purpose: the usual
                    // reason to land here is that the token was cancelled, and passing it on would
                    // abandon the rollback and replace the original exception with a cancellation.
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            },
            cancellationToken);
    }
}

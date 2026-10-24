using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// Fails one write command with a serialization failure, which a retrying execution strategy
/// treats as transient and answers by replaying the whole operation.
/// </summary>
/// <param name="failingWrite">Which write fails, counting from 1; every later one runs normally.</param>
internal sealed class TransientFailureInjector(int failingWrite) : DbCommandInterceptor
{
    private const string SerializationFailure = "40001";

    private int _writes;

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        FailIfDue(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FailIfDue(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void FailIfDue(DbCommand command)
    {
        if (command.CommandText.Contains("INSERT", StringComparison.Ordinal)
            && Interlocked.Increment(ref _writes) == failingWrite)
        {
            throw new PostgresException("injected", "ERROR", "ERROR", SerializationFailure);
        }
    }
}

using EntityFramework.Exceptions.Common;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Application;
using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.SharedKernel.Infrastructure;

/// <summary>
/// Turns the database exceptions that describe a state conflict, rather than a fault, into a
/// <see cref="ConflictError"/>.
/// </summary>
/// <remarks>
/// A unique violation here is the race a handler's own "is this taken?" check lost; that check is
/// what gives the user a field-level error. Mapping a constraint back to a form field would tie
/// database names to property names, so this stays a conflict.
/// </remarks>
public sealed class DatabaseExceptionTranslator : IExceptionTranslator
{
    /// <summary>The code for a write that lost an optimistic-concurrency race.</summary>
    public const string ConcurrencyConflictCode = "Data.ConcurrencyConflict";

    /// <summary>The code for a write that broke a unique constraint.</summary>
    public const string DuplicateKeyCode = "Data.DuplicateKey";

    /// <inheritdoc />
    public AppError? Translate(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => new ConflictError(
            ConcurrencyConflictCode,
            "Someone else changed this while you were working on it. Reload and try again."),
        UniqueConstraintException => new ConflictError(
            DuplicateKeyCode,
            "Something with the same details already exists."),
        _ => null,
    };
}

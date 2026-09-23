using EntityFramework.Exceptions.Common;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Application.Errors;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.SharedKernel.InfrastructureTests;

/// <summary>Tests for <see cref="DatabaseExceptionTranslator"/>.</summary>
public class DatabaseExceptionTranslatorTests
{
    private readonly DatabaseExceptionTranslator _translator = new();

    [Fact]
    public void Translate_a_lost_concurrency_race_is_a_conflict()
    {
        // Act
        var error = _translator.Translate(new DbUpdateConcurrencyException("stale"));

        // Assert
        Assert.Equal(DatabaseExceptionTranslator.ConcurrencyConflictCode, Assert.IsType<ConflictError>(error).Code);
    }

    [Fact]
    public void Translate_a_unique_violation_is_a_conflict()
    {
        // Act
        var error = _translator.Translate(new UniqueConstraintException("duplicate", innerException: null));

        // Assert
        Assert.Equal(DatabaseExceptionTranslator.DuplicateKeyCode, Assert.IsType<ConflictError>(error).Code);
    }

    [Fact]
    public void Translate_any_other_database_failure_is_left_unexpected()
    {
        // Act
        var error = _translator.Translate(new CannotInsertNullException("null", innerException: null));

        // Assert
        Assert.Null(error);
    }
}

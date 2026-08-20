using FluentResults;

namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>
/// A failed input validation, produced by the mediator's validation behaviour from a
/// FluentValidation failure and carried on a failed <see cref="Result"/>.
/// </summary>
/// <remarks>
/// In the Application layer so a handler can return one too, not only the behaviour — a uniqueness
/// check that reads as a field error should not have to invent its own error type. Blazor components
/// still reach it through their feature's Application project.
/// </remarks>
public sealed class ValidationError : Error
{
    /// <summary>Creates an error for one failed rule.</summary>
    /// <param name="propertyName">The command or query property the rule was declared on.</param>
    /// <param name="errorMessage">The rule's human-readable failure message.</param>
    public ValidationError(string propertyName, string errorMessage)
        : base(errorMessage)
    {
        PropertyName = propertyName;
        Metadata[nameof(PropertyName)] = propertyName;
    }

    /// <summary>The command or query property the failed rule was declared on.</summary>
    public string PropertyName { get; }
}

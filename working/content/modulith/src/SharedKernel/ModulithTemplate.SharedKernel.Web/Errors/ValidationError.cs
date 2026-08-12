using FluentResults;

namespace ModulithTemplate.SharedKernel.Web.Errors;

/// <summary>
/// A failed input validation, produced by the mediator's validation behaviour from a
/// FluentValidation failure and carried on a failed <see cref="Result"/>.
/// </summary>
/// <remarks>
/// Lives here rather than beside the behaviour so a feature's Blazor components — which reference
/// <c>ModulithTemplate.SharedKernel.Web</c> but not the host — can pattern-match the errors coming back
/// from <c>IMediator</c> and bind them to the fields they belong to.
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

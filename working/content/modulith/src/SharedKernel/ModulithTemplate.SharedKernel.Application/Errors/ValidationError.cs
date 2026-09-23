namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>
/// An input the user can fix by changing one field — produced by the validation behaviour from a
/// FluentValidation failure, or by a handler for a rule such as "that name is already taken".
/// </summary>
public sealed class ValidationError : AppError
{
    /// <summary>Creates an error for one failed rule.</summary>
    /// <param name="propertyName">The command or query property the rule concerns.</param>
    /// <param name="code">The rule's stable identifier.</param>
    /// <param name="message">The rule's user-facing failure message.</param>
    public ValidationError(string propertyName, string code, string message)
        : base(code, message)
    {
        PropertyName = propertyName;
        Metadata[nameof(PropertyName)] = propertyName;
    }

    /// <summary>The command or query property the failed rule concerns.</summary>
    public string PropertyName { get; }
}

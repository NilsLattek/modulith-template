namespace ModulithTemplate.SharedKernel.Domain.Exceptions;

/// <summary>
/// A business rule refused an operation the user was entitled to attempt; shown to the user.
/// </summary>
/// <remarks>
/// Not for malformed input the form already prevents: that stays an <see cref="ArgumentException"/>
/// guard, reported as a generic failure. The mediator pipeline turns this into a failed result whose
/// error carries <see cref="Code"/> and <see cref="Parameters"/>, so the UI can localize it.
/// </remarks>
#pragma warning disable RCS1194 // The standard constructors would allow a violation without a code
public class BusinessException : Exception
#pragma warning restore RCS1194
{
    private readonly Dictionary<string, object?> _parameters = new(StringComparer.Ordinal);

    /// <summary>Creates a violation of the rule identified by <paramref name="code"/>.</summary>
    /// <param name="code">Stable <c>"Feature:Name"</c> key, declared in the feature's <c>XxxErrorCodes</c>.</param>
    /// <param name="message">English text, shown when no translation exists and written to the log.</param>
    /// <param name="innerException">The fault that revealed the violation, if any.</param>
    public BusinessException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
    }

    /// <summary>The stable key a translation is looked up by.</summary>
    public string Code { get; }

    /// <summary>Values for the placeholders in the translated message, such as <c>{Name}</c>.</summary>
    public IReadOnlyDictionary<string, object?> Parameters => _parameters;

    /// <summary>Adds a value for a placeholder in the translated message.</summary>
    /// <param name="name">The placeholder's name.</param>
    /// <param name="value">Its value.</param>
    /// <returns>This exception, so it can be thrown in one expression.</returns>
    public BusinessException WithParameter(string name, object? value)
    {
        _parameters[name] = value;
        return this;
    }
}

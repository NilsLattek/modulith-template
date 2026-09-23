using FluentResults;

namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>
/// An expected failure a handler reports in a failed <see cref="Result"/>, identified by a stable
/// <see cref="Code"/>.
/// </summary>
/// <remarks>
/// A closed set: <see cref="ValidationError"/>, <see cref="NotFoundError"/>, <see cref="ConflictError"/>
/// and <see cref="UnexpectedError"/>. Anything else on a failed result is treated as unexpected.
/// </remarks>
public abstract class AppError : Error
{
    /// <summary>Creates an error.</summary>
    /// <param name="code">The stable identifier, e.g. <c>Orders.AlreadyShipped</c>.</param>
    /// <param name="message">The default, human-readable description.</param>
    private protected AppError(string code, string message)
        : base(message)
    {
        Code = code;
        Metadata[nameof(Code)] = code;
    }

    /// <summary>
    /// The stable identifier logs record and a localization pass keys on; the message may change,
    /// this may not.
    /// </summary>
    public string Code { get; }
}

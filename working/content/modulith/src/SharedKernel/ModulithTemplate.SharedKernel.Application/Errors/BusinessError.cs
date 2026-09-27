using FluentResults;

using ModulithTemplate.SharedKernel.Domain.Exceptions;

namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>
/// A business rule violation carried on a failed <see cref="Result"/>; its message is safe to show.
/// </summary>
/// <remarks>
/// Produced by the exception behaviour from a <see cref="BusinessException"/>. Unlike an
/// <see cref="ExceptionalError"/>, whose message may leak internals, this one is meant for the user.
/// </remarks>
public sealed class BusinessError : Error
{
    /// <summary>Creates an error from the exception that raised it.</summary>
    /// <param name="exception">The violation.</param>
    public BusinessError(BusinessException exception)
        : base(exception.Message)
    {
        Code = exception.Code;
        Parameters = new Dictionary<string, object?>(exception.Parameters, StringComparer.Ordinal);
        Metadata[nameof(Code)] = Code;
        foreach (var (name, value) in Parameters)
        {
            Metadata[name] = value!;
        }
    }

    /// <summary>The stable key a translation is looked up by.</summary>
    public string Code { get; }

    /// <summary>Values for the placeholders in the translated message.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

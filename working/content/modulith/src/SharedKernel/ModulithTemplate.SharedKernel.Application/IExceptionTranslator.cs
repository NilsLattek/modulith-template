using ModulithTemplate.SharedKernel.Application.Errors;

namespace ModulithTemplate.SharedKernel.Application;

/// <summary>
/// Recognises an exception an outer layer throws for an <i>expected</i> reason and names the error
/// it means, so the exception behaviour need not reference that layer.
/// </summary>
/// <remarks>Infrastructure maps database exceptions this way; register with <c>TryAddEnumerable</c>.</remarks>
public interface IExceptionTranslator
{
    /// <summary>Translates an exception, if this translator recognises it.</summary>
    /// <param name="exception">What the handler threw.</param>
    /// <returns>The error it means, or <see langword="null"/> to leave it unexpected.</returns>
    AppError? Translate(Exception exception);
}

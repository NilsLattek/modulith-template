namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>
/// Something failed that the application did not anticipate — a bug, or an infrastructure fault.
/// </summary>
/// <remarks>
/// Deliberately carries no exception or exception text: that is logged under <see cref="TraceId"/>
/// and never travels to the caller, where it could reach a page.
/// </remarks>
public sealed class UnexpectedError : AppError
{
    /// <summary>The <see cref="AppError.Code"/> every unexpected error carries.</summary>
    public const string UnexpectedCode = "Unexpected";

    /// <summary>Creates the error.</summary>
    /// <param name="traceId">The trace the failure was logged under, if one was active.</param>
    public UnexpectedError(string? traceId)
        : base(UnexpectedCode, "An unexpected error occurred.")
    {
        TraceId = traceId;
    }

    /// <summary>The trace the exception was logged under; a support reference for the user.</summary>
    public string? TraceId { get; }
}

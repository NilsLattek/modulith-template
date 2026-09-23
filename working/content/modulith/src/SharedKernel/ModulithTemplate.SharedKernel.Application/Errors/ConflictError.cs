namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>
/// The request is well-formed, but the current state forbids it — the order has shipped, or
/// someone else changed the record first. No edit to the input fixes it.
/// </summary>
/// <remarks>The message is shown to the user verbatim, so write it as UI copy.</remarks>
/// <param name="code">The stable identifier, e.g. <c>Orders.AlreadyShipped</c>.</param>
/// <param name="message">The user-facing description.</param>
public sealed class ConflictError(string code, string message) : AppError(code, message);

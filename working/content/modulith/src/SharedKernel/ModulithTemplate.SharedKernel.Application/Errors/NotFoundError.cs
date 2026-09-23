namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>The thing the request names does not exist.</summary>
/// <remarks>The message is shown to the user verbatim, so write it as UI copy.</remarks>
/// <param name="code">The stable identifier, e.g. <c>Orders.OrderNotFound</c>.</param>
/// <param name="message">The user-facing description.</param>
public sealed class NotFoundError(string code, string message) : AppError(code, message);

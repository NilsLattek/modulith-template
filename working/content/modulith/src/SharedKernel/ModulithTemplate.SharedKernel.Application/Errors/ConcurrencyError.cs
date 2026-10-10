using FluentResults;

using ModulithTemplate.SharedKernel.Domain.Exceptions;

namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>A change refused because what it was made from changed elsewhere; the caller should reload.</summary>
/// <remarks>Produced by the exception behaviour from a <see cref="ConcurrencyException"/>.</remarks>
public sealed class ConcurrencyError() : Error("This changed elsewhere.");

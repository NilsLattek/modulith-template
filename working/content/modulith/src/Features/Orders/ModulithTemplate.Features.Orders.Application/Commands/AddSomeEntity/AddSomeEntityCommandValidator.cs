using FluentValidation;

using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>
/// Validates <see cref="AddSomeEntityCommand"/> before its handler runs.
/// </summary>
/// <remarks>
/// Deliberately shallow: shape only. Anything depending on domain state (is this name already
/// taken?) belongs in the entity or a domain service, where it holds for every caller.
/// </remarks>
public sealed class AddSomeEntityCommandValidator : AbstractValidator<AddSomeEntityCommand>
{
    /// <summary>Declares the command's rules.</summary>
    public AddSomeEntityCommandValidator() =>
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(SomeEntity.NameMaxLength);
}

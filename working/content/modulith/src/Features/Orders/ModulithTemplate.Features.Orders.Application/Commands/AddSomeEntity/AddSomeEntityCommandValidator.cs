using FluentValidation;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>
/// Validates <see cref="AddSomeEntityCommand"/> before its handler runs.
/// </summary>
/// <remarks>
/// Deliberately shallow: a validator checks the shape of the incoming values — required, length,
/// range, format — and nothing else. Anything that depends on domain state (does this order still
/// accept lines? is this name already taken?) belongs in the entity or a domain service, where it
/// is enforced for every caller rather than only for this one command.
/// </remarks>
public sealed class AddSomeEntityCommandValidator : AbstractValidator<AddSomeEntityCommand>
{
    /// <summary>Declares the command's rules.</summary>
    public AddSomeEntityCommandValidator() =>
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);
}

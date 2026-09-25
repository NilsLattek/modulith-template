using System.ComponentModel.DataAnnotations;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

namespace ModulithTemplate.Features.Orders.Web.Components;

/// <summary>What a user types into <c>SomeEntityForm</c>: instant feedback before a command is sent.</summary>
/// <remarks>
/// Copies <c>SomeEntity</c>'s invariants by value, since Web cannot see the domain. The command has
/// no validator because of this check; the entity still enforces the rules if the copy drifts.
/// </remarks>
[ValidatableType]
public sealed class SomeEntityFormModel : IValidatableObject
{
    private const int AmountScale = 2;

    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(200, ErrorMessage = "A name may be at most 200 characters.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter an amount.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "The amount must be between 0.01 and 9,999,999,999,999,999.99.")]
    public decimal? Amount { get; set; }

    /// <summary>Builds the command; call only once the form has validated, which rules out a null amount.</summary>
    public AddSomeEntityCommand ToAddSomeEntityCommand() => new(Name, Amount.GetValueOrDefault());

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Amount is { } amount && decimal.Round(amount, AmountScale) != amount)
        {
            yield return new ValidationResult(
                $"An amount may have at most {AmountScale} decimal places.", [nameof(Amount)]);
        }
    }
}

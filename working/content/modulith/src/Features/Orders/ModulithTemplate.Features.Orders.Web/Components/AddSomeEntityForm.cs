using System.ComponentModel.DataAnnotations;

using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

namespace ModulithTemplate.Features.Orders.Web.Components;

/// <summary>The add form's model: instant feedback while typing, before a command is sent.</summary>
/// <remarks>
/// Copies <c>SomeEntity</c>'s invariants by value, since Web cannot see the domain. The copy is for
/// the user's convenience only; the command validator and the entity stay authoritative, and their
/// errors still land on these fields if the two drift apart.
/// </remarks>
public sealed class AddSomeEntityForm : IValidatableObject
{
    private const int AmountScale = 2;

    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(200, ErrorMessage = "A name may be at most 200 characters.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter an amount.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "The amount must be between 0.01 and 9,999,999,999,999,999.99.")]
    public decimal? Amount { get; set; }

    public AddSomeEntityCommand ToCommand() => new(Name, Amount.GetValueOrDefault());

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

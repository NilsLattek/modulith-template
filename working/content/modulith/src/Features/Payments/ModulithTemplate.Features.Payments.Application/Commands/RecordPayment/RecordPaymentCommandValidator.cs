using FluentValidation;

using ModulithTemplate.Features.Payments.Domain.Entities;

namespace ModulithTemplate.Features.Payments.Application.Commands.RecordPayment;

/// <summary>Checks the shape of a <see cref="RecordPaymentCommand"/> built from another feature's event.</summary>
public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(Payment.MaxAmount)
            .PrecisionScale(Payment.AmountPrecision, Payment.AmountScale, ignoreTrailingZeros: true);
    }
}

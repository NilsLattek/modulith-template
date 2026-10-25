using FluentResults;

using Mediator;

using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.Features.Payments.Domain.Entities;

namespace ModulithTemplate.Features.Payments.Application.Commands.RecordPayment;

/// <summary>Handles <see cref="RecordPaymentCommand"/>.</summary>
/// <remarks>
/// No "already recorded?" check: the inbox applies each event to this feature exactly once, and its
/// transaction commits this write together with the row's completion.
/// </remarks>
/// <param name="repository">The Payments feature's repository.</param>
public sealed class RecordPaymentCommandHandler(IPaymentsRepository<Payment> repository)
    : ICommandHandler<RecordPaymentCommand, Result>
{
    /// <inheritdoc />
    public async ValueTask<Result> Handle(RecordPaymentCommand command, CancellationToken cancellationToken)
    {
        await repository.AddAsync(Payment.Record(command.OrderId, command.Amount), cancellationToken);
        return Result.Ok();
    }
}

using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Payments.Application.Commands.RecordPayment;

/// <summary>Records a payment against an order Orders announced.</summary>
/// <remarks>Sent by <c>SomeEntityAddedInboxHandler</c>, once per event.</remarks>
/// <param name="OrderId">The order being paid.</param>
/// <param name="Amount">The amount paid.</param>
public sealed record RecordPaymentCommand(Guid OrderId, decimal Amount) : ICommand<Result>;

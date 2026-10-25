using FluentResults;

using Mediator;

using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Payments.Application.Commands.RecordPayment;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Infrastructure.Events;

namespace ModulithTemplate.Features.Payments.Infrastructure.InboxHandlers;

/// <summary>Payments' reaction to <see cref="SomeEntityAddedIntegrationEvent"/>: record a payment.</summary>
/// <param name="services">The provider of the scope the inbox processes the message in.</param>
public sealed class SomeEntityAddedInboxHandler(IServiceProvider services)
    : IntegrationEventInboxTransport<SomeEntityAddedIntegrationEvent, PaymentsContext>(services)
{
    /// <inheritdoc />
    protected override ICommand<Result> ToCommand(SomeEntityAddedIntegrationEvent integrationEvent) =>
        new RecordPaymentCommand(integrationEvent.SomeEntityId, integrationEvent.Amount);
}

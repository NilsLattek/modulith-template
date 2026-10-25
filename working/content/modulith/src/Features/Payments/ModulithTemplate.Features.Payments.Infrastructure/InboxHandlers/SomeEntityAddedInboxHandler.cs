using FluentResults;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.Features.Payments.Application.Commands.RecordPayment;
using ModulithTemplate.Features.Payments.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Infrastructure.Events;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.Features.Payments.Infrastructure.InboxHandlers;

/// <summary>Payments' reaction to <see cref="SomeEntityAddedIntegrationEvent"/>: record a payment.</summary>
/// <param name="inbox">Adds Payments' row.</param>
/// <param name="inboxContext">The shared inbox's context.</param>
/// <param name="scopeFactory">Opens the scope the Command runs in.</param>
public sealed class SomeEntityAddedInboxHandler(
    IInbox inbox, IInboxDbContext inboxContext, IServiceScopeFactory scopeFactory)
    : IntegrationEventInboxHandler<SomeEntityAddedIntegrationEvent, PaymentsContext>(inbox, inboxContext, scopeFactory)
{
    /// <inheritdoc />
    protected override ICommand<Result> ToCommand(SomeEntityAddedIntegrationEvent integrationEvent) =>
        new RecordPaymentCommand(integrationEvent.SomeEntityId, integrationEvent.Amount);
}

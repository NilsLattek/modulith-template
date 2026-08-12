using FluentResults;

using Mediator;

using ModulithTemplate.SharedKernel.Application.Events;
using ModulithTemplate.Features.Orders.Contracts.IntegrationEvents;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>Handles <see cref="AddSomeEntityCommand"/>.</summary>
/// <param name="repository">The Orders feature's repository.</param>
/// <param name="events">Enqueues integration events for dispatch after this command commits.</param>
public sealed class AddSomeEntityCommandHandler(
    IOrdersRepository<SomeEntity> repository,
    IIntegrationEventQueue events)
    : ICommandHandler<AddSomeEntityCommand, Result>
{
    /// <inheritdoc />
    public async ValueTask<Result> Handle(AddSomeEntityCommand command, CancellationToken cancellationToken)
    {
        await repository.AddAsync(new SomeEntity(), cancellationToken);

        // Enqueued, never published from here. The host's IntegrationEventBehaviour flushes the
        // queue only once this handler has returned a successful Result, so no other feature can
        // observe a write that failed — or one that has not committed yet.
        events.Enqueue(new SomeEntityAddedIntegrationEvent(command.Name));
        return Result.Ok();
    }
}

using FluentResults;

using Mediator;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>Handles <see cref="AddSomeEntityCommand"/>.</summary>
/// <param name="repository">The Orders feature's repository.</param>
public sealed class AddSomeEntityCommandHandler(IOrdersRepository<SomeEntity> repository)
    : ICommandHandler<AddSomeEntityCommand, Result>
{
    /// <inheritdoc />
    public async ValueTask<Result> Handle(AddSomeEntityCommand command, CancellationToken cancellationToken)
    {
        await repository.AddAsync(SomeEntity.Create(command.Name), cancellationToken);
        return Result.Ok();
    }
}

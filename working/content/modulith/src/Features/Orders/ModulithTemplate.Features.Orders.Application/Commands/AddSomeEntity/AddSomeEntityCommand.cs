using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>
/// Demonstrates the command shape, including a non-generic <see cref="Result"/> response; replace
/// it with a real command. Throws against a real database, because
/// <see cref="Domain.Entities.SomeEntity"/> is deliberately unmapped in <c>OrdersContext</c>.
/// </summary>
/// <param name="Name">
/// Goes no further than <see cref="AddSomeEntityCommandValidator"/>, and exists only to give that
/// validator a property to demonstrate DTO-level rules on.
/// </param>
public sealed record AddSomeEntityCommand(string Name) : ICommand<Result>;

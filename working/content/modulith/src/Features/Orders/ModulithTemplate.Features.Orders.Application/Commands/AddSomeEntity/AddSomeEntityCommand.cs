using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>
/// Demonstrates the command shape, including a non-generic <see cref="Result"/> response; replace
/// it with a real command.
/// </summary>
/// <param name="Name">
/// The new entity's name. Its <i>shape</i> is checked by <see cref="AddSomeEntityCommandValidator"/>
/// before the handler runs, and again as an invariant inside the entity.
/// </param>
/// <param name="Amount">The amount to record it for, announced to other features.</param>
public sealed record AddSomeEntityCommand(string Name, decimal Amount) : ICommand<Result>;

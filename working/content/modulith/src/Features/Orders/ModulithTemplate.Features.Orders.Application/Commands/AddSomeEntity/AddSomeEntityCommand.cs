using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>
/// Demonstrates the command shape, including a non-generic <see cref="Result"/> response; replace
/// it with a real command.
/// </summary>
/// <remarks>
/// No validator: its only caller is the Orders form, which checks the input server-side, and the
/// entity enforces the same rules as invariants.
/// </remarks>
/// <param name="Name">The new entity's name.</param>
/// <param name="Amount">The amount to record it for, announced to other features.</param>
public sealed record AddSomeEntityCommand(string Name, decimal Amount) : ICommand<Result>;

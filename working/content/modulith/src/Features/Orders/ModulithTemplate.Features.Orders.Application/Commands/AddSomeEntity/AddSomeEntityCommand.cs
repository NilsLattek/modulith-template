using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>
/// Adds an entity to the Orders feature. Replace this with a real command once the feature has
/// one — it exists to demonstrate the command shape, including a non-generic
/// <see cref="Result"/> response. Throws against a real database until
/// <see cref="Domain.Entities.SomeEntity"/> is mapped in <c>OrdersContext</c>, because the
/// placeholder entity is deliberately unmapped.
/// </summary>
public sealed record AddSomeEntityCommand : ICommand<Result>;

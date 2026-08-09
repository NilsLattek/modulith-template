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
/// <param name="Name">
/// A name for the new entity. It goes no further than
/// <see cref="AddSomeEntityCommandValidator"/> — the placeholder entity holds no state — and is
/// here only to give the validator a property to demonstrate DTO-level rules on. Replace the
/// command, its validator and the entity together.
/// </param>
public sealed record AddSomeEntityCommand(string Name) : ICommand<Result>;

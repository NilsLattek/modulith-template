---
name: adding-a-command-or-query
description: Add a CQRS command or query to an existing feature — sizing it to one business operation, then the message, its handler, its validator and the DTO it returns. Use when adding a write operation or a read to a feature, when choosing between per-field commands and one update-everything command, wiring a Blazor page or endpoint to the application layer, deciding where a FluentValidation validator belongs, or mapping an entity to a DTO with Mapperly.
---

# Adding a command or query

Every operation is a message plus its handler, in a folder of its own.

## Sizing the command

Decide the size before creating a folder — once `UpdateXCommand/` exists, the size is settled. A
command is one **business operation**, not a data change. Check three things:

1. **A domain expert would say it.** "Confirm the order", "change the shipping address". A name that
   reduces to `Update<Field>`, `Set<Field>` or `Update<Entity>` describes data, not intent.
2. **It maps to one entity method.** `ConfirmOrder` → `order.Confirm()`, ideally raising
   `OrderConfirmed`. One verb from the command, through the method, to the event.
3. **It changes one aggregate.** Needing a second one means splitting it, reacting to a domain or
   integration event, or a domain service.

Both failure modes follow the data instead of the intent:

- **Too small:** one command per field. The operation the user performed is split across several
  commands, and the rule tying the fields together has nowhere to live.
- **Too big:** one `Update<Entity>Command` carrying the whole DTO. The handler diffs old against new
  to guess what happened, and every rule has to run on every save.

Size follows intent, not field count. One field is fine when the name says why it changes
(`ChangeShippingAddress`). A single `Update<Thing>Details` backed by one `UpdateDetails(...)` method
is fine only where no business rule tells the fields apart — reference data, admin screens, a draft
filled in across wizard steps. Creation is named after the business event too: `PlaceOrder`,
`RegisterCustomer`, not `CreateOrder`.

## Layout

```
Application/
  Commands/PlaceOrder/
    PlaceOrderCommand.cs           public sealed record ... : ICommand<OrderDto>
    PlaceOrderCommandHandler.cs    public sealed class
    PlaceOrderCommandValidator.cs  public sealed class : AbstractValidator<PlaceOrderCommand> (only if needed, below)
  Queries/GetOrderById/
    ...
  Dtos/                            the shapes returned
  Mappers/                         <Entity>Mapper: entity → DTO (below)
```

The validator lives **in the message's own folder**, not a `Validators/` directory. Each feature's
`ConfigureXxxApplication` picks it up via `AddValidatorsFromAssembly`.

## The handler

Orchestrates and nothing more. It acts as the ApplicationService in a DDD architecture: load entities via the repository, call entity methods or a domain
service, persist, map to a DTO. **No `try`/`catch`** — unhandled exceptions become a failed
`Result` centrally, and a `BusinessException` becomes a `BusinessError` the user sees (below).

A handler that decides whether something is *valid*, rather than reacting to the domain's answer,
has taken work that belongs in the entity.

Two rules that fail in non-obvious ways:

- **Always return `Result` / `Result<T>`.** The exception-to-`Result` and validation behaviours are
  constrained to result responses and are silently skipped otherwise.
- **Handlers must be `public`.** The mediator's source generator emits a hard `typeof(<Handler>)`
  reference into the host's compilation, so `internal` breaks the host build with `CS0122`.

`Web` reaches `Application` only through `IMediator` — never by calling a handler directly.

## Mapping to a DTO

Every entity → DTO conversion is a Mapperly mapper: one `internal static partial class
<Entity>Mapper` per entity in `Mappers/`, whose extension methods the handler calls.

```csharp
// Application/Mappers/SomeEntityMapper.cs
[Mapper]
internal static partial class SomeEntityMapper
{
    public static partial SomeEntityDto ToDto(this SomeEntity entity);
}

// In the handler, after loading the entity through a spec
return Result.Ok(entity.ToDto());
```

- **Mappers read entities; they never build them.** A command reaches its entity through a factory
  or entity method (`SomeEntity.Create(...)`, `order.Confirm()`), where the invariants live. A
  DTO → entity mapper failing to compile against a `private` or `internal` constructor is that rule
  holding — keep the constructor as it is.
- **Every DTO property needs a source.** `Mappers/MapperDefaults.cs` sets
  `RequiredMappingStrategy.Target` and `RMG012` is a build error, so a DTO property with nothing to
  map from fails the build. Entity members the DTO leaves out are fine.
- **Members that don't match by name:**
  - a rename: `[MapProperty(nameof(SomeEntity.Amount), nameof(SomeEntityDto.Total))]` on the method;
  - a type conversion (value object → primitive, enum → string): a `private static` method in the
    mapper, which Mapperly uses wherever its parameter and return types match;
  - name each DTO property for its reader and rename with `[MapProperty]`, rather than naming it to
    trigger Mapperly's flattening (`AmountValue`).
- **Map in memory:** load with a spec, then `ToDto()`. A list too large to load whole uses an Ardalis
  `Specification<T, TResult>` with `Select(...)` instead.
- A published `Contracts.Api` DTO is mapped the same way, by another method on the same mapper.

## The validator

Validation is opt-in: a message with no validator passes straight through. The pipeline runs it
before the handler.

**Add one only when the message has a caller without a form** — a minimal API endpoint, an
integration-event consumer, a background job. For those, the validator is the boundary: without it
malformed input reaches the entity, throws, and is logged as an error and returned as an
`ExceptionalError` with no field name.

**A command sent only from a Blazor form gets none**: the form model checks the input on the server
and the entity enforces the same rules, so a validator would write them a third time (skill:
`adding-a-blazor-form`).

**Validators check the shape of incoming values, not business rules** — required, length, range,
format, "these two fields must both be set". That is the whole remit: a malformed DTO is rejected
before a handler touches an entity.

Anything depending on domain state (may this order still be changed? is this quantity legal?) is an
invariant and belongs in the entity or a domain service, where it holds for every caller. **Never
inject a repository into a validator.**

Failures come back as a failed `Result` carrying one `ValidationError` per broken rule
(`ModulithTemplate.SharedKernel.Application/Errors/ValidationError.cs`), each with the `PropertyName`
it was declared on.

## Refusing an operation: `BusinessException`

When a business rule says no to something the user was entitled to try — the name is taken, the
order is already shipped — throw a `BusinessException`. Throw it from the entity or domain service
that owns the rule; the handler throws one only for a check that needs a lookup.

```csharp
// Domain/OrdersErrorCodes.cs — one per feature, "Feature:Name", never renamed once shipped
public static class OrdersErrorCodes
{
    public const string NameAlreadyTaken = "Orders:NameAlreadyTaken";
}

// In the handler, before creating the entity
if (await repository.AnyAsync(new SomeEntityByNameSpec(command.Name), cancellationToken))
{
    throw new BusinessException(OrdersErrorCodes.NameAlreadyTaken, $"The name '{command.Name}' is already taken.")
        .WithParameter("Name", command.Name);
}
```

- **The code** is the key a translation is looked up by; the English message is what shows until
  one exists. Parameters fill the translation's placeholders (`{Name}`).
- **The pipeline** turns it into a failed `Result` carrying a `BusinessError` (`Code`, `Parameters`,
  `Message`), logged as a warning with no stack trace. A component shows that message (skill:
  `adding-a-blazor-form`); every other error stays generic.
- **Not for malformed input.** A blank name or out-of-range amount is an `ArgumentException` guard in
  the entity: the form model stops it first, so reaching the guard means a bug.
- **A check-then-insert races.** Two requests can both pass the lookup above; a unique index is the
  real guarantee, and the check only turns the common case into a friendly message.
- Derive a subclass (`NameAlreadyTakenException : BusinessException`) only when a caller must `catch`
  that one case.

## Calling it from Blazor

Components must not `@inject IMediator` for database work — scoped services live for the whole
SignalR circuit. Inject `IScopedMediator` and `await Mediator.Send(message)`; it gives each message
a scope of its own.

That scope is gone by the time the component renders, so **the handler must return a DTO, never an
entity** — a returned entity's navigation properties throw `ObjectDisposedException` at render time.
A message typed `IQuery<Result<SomeEntity>>` will not compile where a component consumes it: the
feature's `Domain` is referenced with `PrivateAssets="all"` and never reaches the `Web` layer.

## Tests

A command or query that enforces a domain rule gets its test on the **entity**, in
`<Name>.DomainTests` — not only through a handler test. Handler tests cover orchestration:
that the right entity method was called and the right `Result` came back.

Mappers get no tests of their own: the handler test asserting the returned DTO covers them, and a
DTO property with no source already fails the build.

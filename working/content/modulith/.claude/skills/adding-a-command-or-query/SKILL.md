---
name: adding-a-command-or-query
description: Add a CQRS command or query to an existing feature — sizing it to one business operation, then the message, its handler, its validator and the DTO it returns. Use when adding a write operation or a read to a feature, when choosing between per-field commands and one update-everything command, wiring a Blazor page or endpoint to the application layer, or deciding where a FluentValidation validator belongs.
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
  Mappers/                         Mapperly [Mapper] partials
```

The validator lives **in the message's own folder**, not a `Validators/` directory. Each feature's
`ConfigureXxxApplication` picks it up via `AddValidatorsFromAssembly`.

## The handler

Orchestrates and nothing more. It acts as the ApplicationService in a DDD architecture: load entities via the repository, call entity methods or a domain
service, persist, map to a DTO. The happy path plus explicit `Result.Fail` for expected domain
failures. **No `try`/`catch`** — unhandled exceptions become a failed `Result` centrally.

A handler that decides whether something is *valid*, rather than reacting to the domain's answer,
has taken work that belongs in the entity.

Two rules that fail in non-obvious ways:

- **Always return `Result` / `Result<T>`.** The exception-to-`Result` and validation behaviours are
  constrained to result responses and are silently skipped otherwise.
- **Handlers must be `public`.** The mediator's source generator emits a hard `typeof(<Handler>)`
  reference into the host's compilation, so `internal` breaks the host build with `CS0122`.

`Web` reaches `Application` only through `IMediator` — never by calling a handler directly.

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

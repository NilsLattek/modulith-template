---
name: adding-a-command-or-query
description: Add a CQRS command or query to an existing feature — the message, its handler, its validator and the DTO it returns. Use when adding a write operation or a read to a feature, wiring a Blazor page or endpoint to the application layer, or deciding where a FluentValidation validator belongs.
---

# Adding a command or query

Every operation is a message plus its handler, in a folder of its own.

## Layout

```
Application/
  Commands/PlaceOrder/
    PlaceOrderCommand.cs           public sealed record ... : ICommand<OrderDto>
    PlaceOrderCommandHandler.cs    public sealed class
    PlaceOrderCommandValidator.cs  public sealed class : AbstractValidator<PlaceOrderCommand>
  Queries/GetOrderById/
    ...
  Dtos/                            the shapes returned
  Mappers/                         Mapperly [Mapper] partials
```

The validator lives **in the message's own folder**, not a `Validators/` directory. Each feature's
`ConfigureXxxApplication` picks it up via `AddValidatorsFromAssembly`.

## The handler

Orchestrates and nothing more. It acts as the ApplicationService in a DDD architecture: load entities via the repository, call entity methods or a domain
service, persist, map to a DTO. The happy path plus an explicit `Result.Fail` for each expected
failure. **No `try`/`catch`** — unhandled exceptions become an `UnexpectedError` centrally.

A handler that decides whether something is *valid*, rather than reacting to the domain's answer,
has taken work that belongs in the entity.

Two rules that fail in non-obvious ways:

- **Always return `Result` / `Result<T>`.** The exception-to-`Result` and validation behaviours are
  constrained to result responses and are silently skipped otherwise.
- **Handlers must be `public`.** The mediator's source generator emits a hard `typeof(<Handler>)`
  reference into the host's compilation, so `internal` breaks the host build with `CS0122`.

`Web` reaches `Application` only through `IMediator` — never by calling a handler directly.

## Returning a failure

An expected failure is a **Rejection** — return it, never throw it. Pick the type by the definitions
in `CONTEXT.md`; the test is whether the user could fix it by changing a value they typed:

```csharp
if (await repository.AnyAsync(new OrderByNameSpec(command.Name), cancellationToken))
    return Result.Fail(new ValidationError(nameof(command.Name), "Orders.NameTaken", "That name is already taken."));

if (!order.CanBeCancelled)
    return Result.Fail(new ConflictError("Orders.AlreadyShipped", "This order has shipped and can no longer be cancelled."));
```

- **The rule stays in the entity.** The handler asks (`order.CanBeCancelled`) and reports the answer;
  it does not decide. The entity still throws if called anyway — that is the invariant.
- **Codes are `<Feature>.<Reason>`** and never change; logs record them and a localization pass keys on
  them. Messages of `NotFoundError` and `ConflictError` reach the user verbatim — write them as UI copy.
- **A `ValidationError`'s property name is the command's**, so a form can put the message beside the
  field.
- **Database outcomes need no handler code.** A lost concurrency race or a unique-constraint violation
  arrives as a `ConflictError`, translated in `SharedKernel.Infrastructure` by an
  `IExceptionTranslator`. Add another translator there for any other exception that means an expected
  outcome.

## The validator

Validation is opt-in: a message with no validator passes straight through. The pipeline runs it
before the handler.

**Validators check the shape of incoming values, not business rules** — required, length, range,
format, "these two fields must both be set". That is the whole remit: a malformed DTO is rejected
before a handler touches an entity.

Anything depending on domain state (may this order still be changed? is this quantity legal?) is an
invariant and belongs in the entity or a domain service, where it holds for every caller. **Never
inject a repository into a validator.**

Failures come back as a failed `Result` carrying one `ValidationError` per broken rule, each with
the `PropertyName` it was declared on and a `Code` — FluentValidation's validator name
(`NotEmptyValidator`) unless the rule sets `.WithErrorCode(...)`.

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

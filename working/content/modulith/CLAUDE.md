# CLAUDE.md

## Commands

```bash
dotnet restore                                       # then pass --no-restore below
dotnet build --no-restore -warnaserror -v minimal    # CI treats warnings as errors; -v minimal cuts output-path noise
cd src/ModulithTemplate.Web && dotnet run # needs the devcontainer's Postgres

dotnet test --no-restore
dotnet test --no-restore --project test/Features/Orders/ModulithTemplate.Features.Orders.DomainTests
dotnet test --no-restore --project <project> --filter-class "*SomeEntityTests*" # or --filter-method

# EF Core: each feature owns its own DbContext, schema and migrations, so `--context`
# is always required — these wrappers supply it.
bash add-migration.sh Orders AddSomeColumn  # <FeatureName|Outbox> <MigrationName>
bash add-migration.sh Outbox AddSomeColumn  # the shared outbox table (ADR 0001), not a feature
bash update-database.sh                     # applies every context's pending migrations
```

## Architecture

A vertical-slice modular monolith built on DDD. Each feature under `src/Features/<Name>/` owns a set
of layer projects, mirrored by test projects under `test/Features/<Name>/`:

`Domain` has no outbound dependencies and is the only place business rules live; features must not depend on each other; `ModulithTemplate.ArchitectureTests` enforces the layer rules, the cross-feature isolation and the naming/placement conventions below — each in bothdirections, so a `*Spec` outside `Specifications/` fails just as a badly-named type inside it does.

`Contracts` is how one feature reaches another — synchronously through a Module API, asynchronously
through an Integration Event. See *Reaching another feature* below.

### Where business logic goes

**The default answer is: in the entity.** Push behaviour down until it has no lower place to go.

| The rule concerns… | Put it in… |
| --- | --- |
| One entity's own state and invariants | the **entity** — constructor + mutator methods |
| A concept with no identity, defined only by its values (`Money`, `Address`, `EmailAddress`) | a **value object** — `record` in `Domain/ValueObjects/` |
| Consistency between entities inside one aggregate | the **aggregate root**'s methods |
| Several aggregates, or a decision needing a lookup/repository | a **`*DomainService`** in `Domain/Services/` |
| A reusable query predicate | a **`*Spec`** in `Domain/Specifications/` |
| Sequencing: load → call domain → persist → map | the **handler** in `Application` |
| The *shape* of an incoming value — required, length, range, format | a **`*CommandValidator`** beside its command (FluentValidation) |
| HTTP, Blazor, `DbContext`, JSON, configuration | `Web` / `Infrastructure` |

Signals a rule sits in the wrong place: a handler decides whether something is *valid* rather than
reacting to the domain's answer; the same rule is written on both the create and the update path
(duplication means it belongs in the entity, enforced once for all callers); an entity has public
setters and no methods while a handler mutates it property by property; a Blazor component enforces a
rule nothing else does; an entity injects a repository — entities do not reach for data, a domain
service does.

### Writing entities

Entities are rich, not data bags:

- **Private setters**, and collections exposed as `IReadOnlyList<T>` over a private backing list.
  State changes only through named methods that say what happened (`AddLine`, `Confirm`, `Cancel`).
- **`internal` constructor + private parameterless ctor for EF.** Application obtains instances
  through a `public static` factory method or a domain service — never `new`.
- **Invariants are enforced in the constructor and in every mutator**, so no call path can produce an
  invalid entity. The domain throws rather than returning `Result` (it takes no dependency on
  FluentResults); unhandled handler exceptions become a failed `Result` centrally. An *expected*
  failure should still be checked by the handler and returned as an explicit `Result.Fail`.

**When you add or change an invariant, cover it with a `<Name>.DomainTests` test on the entity**, not
only through a handler test — the domain is where the guarantee lives.

### Application layer (CQRS)

Every operation is a `public sealed record` message (`ICommand<T>` / `IQuery<T>`) plus its
`public sealed` handler, in a folder of its own under `Application/Commands/<Name>/` or
`Application/Queries/<Name>/`; DTOs go in `Application/Dtos/`, Mapperly `[Mapper]` partials in
`Application/Mappers/`. A handler orchestrates and nothing more — load entities via the repository,
call entity methods or a domain service, persist, map to a DTO — holding the happy path plus explicit
`Result.Fail` for expected domain failures, and no `try`/`catch`. Two rules easy to get wrong:

- **Handlers always return `Result` / `Result<T>`.** Both the exception-to-`Result` and the validation
  behaviour are constrained to result responses, and are silently skipped otherwise.
- **Handlers must be `public`.** The mediator's source generator emits a hard `typeof(<Handler>)`
  reference into the host's compilation, so marking one `internal` breaks the host build with `CS0122`.
  Do not "tidy" them.

`Web` reaches `Application` only through `IMediator` — never by calling a handler directly.

#### Validating a command or query

The mediator pipeline validates a message before its handler runs. Put a
`public sealed class <Name>CommandValidator : AbstractValidator<<Name>Command>` **in the message's own
folder**; each feature's `ConfigureXxxApplication` picks it up via `AddValidatorsFromAssembly`. A
message with no validator passes straight through, so validation is opt-in.

**Validators check the shape of incoming values, not business rules** — required, length, range,
format, "these two fields must both be set". That is the whole remit: a malformed DTO is rejected
before a handler touches an entity. Anything depending on domain state (may this order still be
changed? is this quantity legal?) is an invariant and belongs in the entity or a domain service, where
it holds for every caller. Never inject a repository into a validator.

Failures come back as a failed `Result` carrying one `ValidationError` per broken rule
(`ModulithTemplate.SharedKernel.Application/Errors/ValidationError.cs`), each with the `PropertyName` it was declared
on, so a Blazor form can group `result.Errors.OfType<ValidationError>()` by property and bind the
messages to their fields.

### Reaching another feature

Both routes go through the publishing feature's `Contracts` project — the only part of a feature a
sibling may reference.

| You need… | Use |
| --- | --- |
| An answer, now, to finish the request you are in | the **Module API** — `IOrdersApi` in `Orders.Contracts/Api/`, implemented in `Orders.Application/Api/`. Synchronous, read-only, the caller's transaction. |
| Something to happen elsewhere *because* something happened here | an **Integration Event** — durable, at least once, each consumer in its own transaction. |

Publish an Integration Event when you do not need the answer to proceed: the publisher never learns
who reacted, or whether they succeeded.

#### Publishing one

Where the pieces live, in the shipped `Orders` → `Payments` example:

| Piece | Lives in |
| --- | --- |
| `SomeEntityAddedDomainEvent`, raised by the aggregate's mutator | `Orders.Domain/Events/` |
| `SomeEntityAddedIntegrationEvent : IIntegrationEvent` — the published contract | `Orders.Contracts/Events/` |
| The handler translating the one into the other | `Orders.Application/DomainEventHandlers/` |
| `services.AddIntegrationEvent<SomeEntityAddedIntegrationEvent>()` | `ConfigureOrdersApplication` |
| `SomeEntityAddedIntegrationEventHandler : INotificationHandler<T>` | `Payments.Application/IntegrationEventHandlers/` |

The translating handler hands the event to its feature's `I<Name>IntegrationEventPublisher`, which
**stages** an outbox row through that feature's `DbContext`: the row is written by the same save that
persists the aggregate, so the event exists if and only if the change committed. A worker then
republishes it in-process, each consumer in a fresh scope with its own `DbContext`. No feature code
opens a transaction. The marker interface and its binding in `<Name>Module.cs` are scaffolded, so
publishing costs a record, the translating handler, and the one registration line — which lives in
`Application` because that is the only layer allowed to reference `Contracts`, and is what lets the
worker turn a stored row back into the event.

`EventId` and `GroupKey` are on the event itself, because republishing hands a consumer the event
and nothing else. **The Group key is the aggregate the event concerns** — events sharing one are
delivered in order, one at a time, while unrelated aggregates proceed concurrently; a constant would
serialise the application behind a single stuck message. The architecture tests fail an
`IIntegrationEvent` declared outside a `Contracts` assembly, or named or placed off-convention.

#### Consuming one, and what idempotency has to mean

Consuming costs a `ProjectReference` from the consumer's `Application` to the publisher's
`Contracts`, and the handler — the consumer registers nothing, and neither side mentions the other
anywhere else.

Delivery is **at least once**, in a stronger sense than that phrase usually carries: one message fans
out to every consumer, so a consumer that throws fails the *message*. It is redelivered, re-running
consumers that already succeeded — and consumers ordered after the failing one may not have run at
all. A handler must tolerate re-running because a **sibling** failed, not merely after its own
failure.

So check before you write. The shipped consumer asks `PaymentForOrderSpec` whether this order already
has a payment and returns if it does; a natural key, or a unique index to write against, is the
cheapest route. `EventId` is there for when no natural key exists — key your own row on it.

**A consumer that genuinely cannot be idempotent** — an external, unrepeatable effect like charging a
card — keeps that effect out of the fan-out handler:

1. The handler consuming the shared event does only idempotent work: through its own `DbContext` it
   writes an inbox row keyed on `EventId` (unique index), and returns if the row is already there.
2. The effect hangs off that row's own save — a domain event on the inbox entity, or this feature's
   own integration event with exactly one consumer — so a sibling's failure can never re-run it.

The effect is then retried only if it itself failed, which is the floor: at-least-once cannot be made
exactly-once outside the database. Do not use the shared outbox as an inbox — one row per event id
would let the first consumer to complete starve the rest.

#### Two things that bite

- **Renaming or moving an Integration Event is a breaking change.** Every outbox row stores the
  event's full type name and the worker resolves the type back from it, so rows written under the old
  name can no longer be delivered: they fail forever, blocking their Group, logging
  `No integration event is registered as '<old name>'` and a repeated-failure warning. The namespace
  and type name are the wire contract — rename only against a drained outbox.
- **An Integration Event with no consumer fails the build.** The mediator's source generator reports
  it against the host as `MSG0005` — a warning, so `-warnaserror` is what turns it into the build
  failure CI sees. Deliberate: in a monolith every consumer is in the same solution, so "nobody
  listens to this" is a defect rather than a deployment state, and the alternative is an event
  dispatched to nobody at runtime. Write the publishing and consuming sides together.

### Infrastructure

EF Core + Npgsql, owned entirely by the feature: a concrete `DbContext` (schema set via
`HasDefaultSchema`), a context-bound `<Name>Repository<T> : RepositoryBase<T>, I<Name>Repository<T>`,
and its own `Data/Migrations/`. Register the context through
`ModulithTemplate.SharedKernel.Infrastructure`'s `AddModuleDbContext<TContext>(configuration, schema)`, which
carries the snake_case `EFCore.NamingConventions` setup; that shared project defines EF conventions
only and never a concrete `DbContext`.

### Dependency injection

Register a service in the owning layer's `Configuration.cs` — not in `Program.cs`, and not in another
feature's composition root. **Handlers are the one exception**: the host's `AddMediator` discovers them
automatically.

### Database access from Blazor components

Scoped services live for the whole SignalR circuit in Blazor Server, so a directly-injected scoped
dependency — `IMediator` included — shares one long-lived, non-thread-safe `DbContext` for the entire
user session. **Components must not `@inject` `IMediator` for database work**: inject
`IServiceScopeFactory` and send each message inside `ScopeFactory.WithNewScopeAsync(...)` from
`ModulithTemplate.SharedKernel.Web/Extensions/ServiceScopeExtensions.cs`. That needs `@using Mediator` and
`@using ModulithTemplate.SharedKernel.Web.Extensions` — neither is in `_Imports.razor`. Stateless, non-DB
services may stay directly injected.

## Conventions

- **Tests**: xUnit v3 on Microsoft.Testing.Platform, **NSubstitute** for substitutes, **bUnit** for
  Blazor component tests. Shared settings and common test packages come from
  `test/Directory.Build.props`, so a test `.csproj` normally holds nothing but a `ProjectReference`.
- **Keep them short.** An XML `<summary>` is a line or two. A `<remarks>` or an inline comment earns its space only by recording what the code cannot say. Two tight lines beat a well-written paragraph; if a comment runs past a few lines, cut it rather than polishing it.

## Adding a feature

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Shipping
```

Run this from the solution root (the directory containing the `.slnx`). It creates the feature's
layer and test projects, adds them all to the solution, and mirrors the `Orders` persistence and DI
scaffolding. **The one manual step** is registering the feature with the host: add a project reference
from `src/ModulithTemplate.Web` to the feature's `.Web` project, and call
`builder.ConfigureShippingFeature();` in `Program.cs`. Then model the feature's entities under
`Shipping.Domain/Entities/` and create its first migration with
`bash add-migration.sh Shipping InitialShipping`.

`Orders` and `Payments` are both scaffolded this way; `Payments` is the worked example of the
registration step above.

## MCP servers

- **mslearn** — look up current .NET / C# APIs. This solution targets the latest .NET, so avoid
  writing outdated C#.

## Additional Tools

@.claude/RTK.md

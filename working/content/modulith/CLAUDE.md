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
bash add-migration.sh Orders InitialOrders  # <FeatureName|Outbox> <MigrationName>
bash add-migration.sh Outbox AddSomeColumn  # the shared outbox table (ADR 0001), not a feature
bash update-database.sh                     # applies every context's pending migrations
```

## Architecture

A vertical-slice modular monolith built on DDD. Each feature under `src/Features/<Name>/` owns a set
of layer projects, mirrored by test projects under `test/Features/<Name>/`:

`Domain` has no outbound dependencies and is the only place business rules live; features must not depend on each other; `ModulithTemplate.ArchitectureTests` enforces the layer rules, the cross-feature isolation and the naming/placement conventions below — each in bothdirections, so a `*Spec` outside `Specifications/` fails just as a badly-named type inside it does.

`Contracts` is how one feature reaches another.

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
dotnet new modulith-feature --appName ModulithTemplate -n Payments
```

Run this from the solution root (the directory containing the `.slnx`). It creates the feature's
layer and test projects, adds them all to the solution, and mirrors the `Orders` persistence and DI
scaffolding. **The one manual step** is registering the feature with the host: add a project reference
from `src/ModulithTemplate.Web` to the feature's `.Web` project, and call
`builder.ConfigurePaymentsFeature();` in `Program.cs`. Then model the feature's entities under
`Payments.Domain/Entities/` and create its first migration.

## MCP servers

- **mslearn** — look up current .NET / C# APIs. This solution targets the latest .NET, so avoid
  writing outdated C#.

## Additional Tools

@.claude/RTK.md

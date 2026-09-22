# CLAUDE.md

## Commands

```bash
dotnet restore                                       # then pass --no-restore below
dotnet build --no-restore -warnaserror -v minimal    # CI treats warnings as errors; -v minimal cuts output-path noise
cd src/ModulithTemplate.Web && dotnet run # needs the devcontainer's Postgres

dotnet test --no-restore
dotnet test --no-restore --project test/SharedKernel/ModulithTemplate.SharedKernel.DomainTests
dotnet test --no-restore --project <project> --filter-class "*SomeEntityTests*" # or --filter-method

# EF Core: each feature owns its own DbContext, schema and migrations, so `--context`
# is always required — these wrappers supply it.
bash add-migration.sh Orders AddSomeColumn  # <FeatureName|Outbox> <MigrationName>
bash add-migration.sh Outbox AddSomeColumn  # the shared outbox table, not a feature
bash update-database.sh                     # applies every context's pending migrations
```

## Architecture

A vertical-slice modular monolith built on DDD. Each feature under `src/Features/<Name>/` owns a set
of layer projects, mirrored by test projects under `test/Features/<Name>/`. `CONTEXT.md` is the
glossary for the terms used throughout.

`Domain` has no outbound dependencies and is the only place business rules live. **Features must not
depend on each other** — `Contracts` is the only crossing point, reached through a Module API or an
Integration Event (skill: `reaching-another-feature`). `ModulithTemplate.ArchitectureTests` enforces
the layer rules.

**A feature is a transaction boundary, sized like a bounded context — not a folder, a screen or a
CRUD table.** Two features share no transaction, so the split cannot be undone cheaply. The default
for new work is to put it in an existing feature; creating one needs positive justification (skill:
`adding-a-feature`, which covers the sizing test and the scaffold). Adding a command or query to an
existing feature has its own layout and validator rules (skill: `adding-a-command-or-query`).

### Where business logic goes

**The default answer is: in the entity.** Push behaviour down until it has no lower place to go. Follow DDD best practices.

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
`public sealed` handler, in a folder of its own. A handler orchestrates and nothing more, with no
`try`/`catch`. Two rules that fail in non-obvious ways:

- **Handlers always return `Result` / `Result<T>`.** Both the exception-to-`Result` and the validation
  behaviour are constrained to result responses, and are silently skipped otherwise.
- **Handlers must be `public`.** The mediator's source generator emits a hard `typeof(<Handler>)`
  reference into the host's compilation, so marking one `internal` breaks the host build with `CS0122`.
  Do not "tidy" them.

`Web` reaches `Application` only through `IMediator` — never by calling a handler directly.
Validators check the *shape* of incoming values, never business rules, and never inject a repository.

### Dependency injection

Register a service in the owning layer's `Configuration.cs` — not in `Program.cs`, and not in another
feature's composition root.

### Database access from Blazor components

Scoped services live for the whole SignalR circuit in Blazor Server, so a directly-injected scoped
dependency — `IMediator` included — shares one long-lived, non-thread-safe `DbContext` for the entire
user session. **Components must not `@inject` `IMediator` for database work**: inject
`IScopedMediator` and `await Mediator.Send(message)`. It gives each message a DI scope of its own and
disposes it when the message completes. Stateless, non-DB services may stay directly injected.

**Handlers return DTOs, never entities.** That scope is disposed before the component renders, so a
returned entity is attached to a dead `DbContext` and reading a navigation property throws
`ObjectDisposedException` — at render time, long after the query passed. Value objects are fine;
`ModulithTemplate.ArchitectureTests` enforces the rule.

`ServiceScopeExtensions.WithNewScopeAsync(...)` remains for scoped UI work that is not a message.

## Conventions

- **Tests**: xUnit v3 on Microsoft.Testing.Platform, **NSubstitute** for substitutes, **bUnit** for
  Blazor component tests.
- **Keep them short.** An XML `<summary>` is a line or two. A `<remarks>` or an inline comment earns its space only by recording what the code cannot say. Two tight lines beat a well-written paragraph; if a comment runs past a few lines, cut it rather than polishing it.

## MCP servers

- **mslearn** — look up current .NET / C# APIs. This solution targets the latest .NET, so avoid
  writing outdated C#.

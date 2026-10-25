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
bash add-migration.sh Orders AddSomeColumn  # <FeatureName|Outbox|Inbox> <MigrationName>
bash add-migration.sh Outbox AddSomeColumn  # the shared outbox table, not a feature
bash add-migration.sh Inbox AddSomeColumn   # the shared inbox table, likewise
bash update-database.sh                     # applies every context's pending migrations
```

## Architecture

A vertical-slice modular monolith built on DDD. Each feature under `src/Features/<Name>/` owns a set
of layer projects, mirrored by test projects under `test/Features/<Name>/`. `CONTEXT.md` is the
glossary for the terms used throughout.

`Domain` is the only place business rules live. **Features never reference each other** — reach
another one through its `Contracts` (skill: `reaching-another-feature`). `ModulithTemplate.ArchitectureTests`
enforces the layer, naming and entity-shape rules, and its failure messages say how to fix a violation.

**A feature is a transaction boundary, sized like a bounded context — not a folder, a screen or a
CRUD table.** New work goes in an existing feature unless a new one is justified (skill:
`adding-a-feature`); a new command or query follows `adding-a-command-or-query`.

### Where business logic goes

**The default answer is: in the entity.** Push behaviour down until it has no lower place to go.

| The rule concerns… | Put it in… |
| --- | --- |
| One entity's own state and invariants | the **entity** — constructor + mutator methods |
| A concept with no identity, defined only by its values (`Money`, `Address`, `EmailAddress`) | a **value object** — `record` in `Domain/ValueObjects/` |
| Consistency between entities inside one aggregate | the **aggregate root**'s methods |
| A rule spanning several aggregates | a **`*DomainService`** in `Domain/Services/`, given the entities its handler loaded |
| A reusable query predicate | a **`*Spec`** in `Domain/Specifications/` |
| Sequencing: load → call domain → persist → map | the **handler** in `Application` |
| The *shape* of input typed into a Blazor form — for instant feedback | a **`*FormModel`** (DataAnnotations) beside its form in `Web` |
| The *shape* of a message from a caller with no form — an endpoint, a consumer, a job | a **`*CommandValidator`** beside its command (FluentValidation) |
| HTTP, Blazor, `DbContext`, JSON, configuration | `Web` / `Infrastructure` |

Signals a rule sits in the wrong place: a handler decides whether something is *valid* rather than
reacting to the domain's answer; the same rule is written on both the create and the update path; an
entity has public setters while a handler mutates it property by property; a Blazor component
enforces a rule nothing else does.

### Writing entities

Entities are rich, not data bags: private setters, named methods that say what happened (`Confirm`),
a factory instead of `new`, other aggregates referenced by id. The architecture tests check that
shape; they cannot check these:

- **Invariants are enforced in the constructor and in every mutator**, so no call path can produce an
  invalid entity.
- **A rule the user can break and should be told about throws `BusinessException`** with a
  `"Feature:Name"` code, not `ArgumentException`.
- **Cover a new or changed invariant with a test on the entity** in `<Name>.DomainTests`, not only
  through a handler test.

### Application layer (CQRS)

Every operation is a `public sealed record` message (`ICommand<T>` / `IQuery<T>`) plus its
`public sealed` handler, in a folder of its own. A handler orchestrates and nothing more, with no
`try`/`catch`.

**The handler loads, the domain decides.** The handler resolves ids into entities through
repositories and specs and passes the entities down; an entity or domain service never injects a
repository.

**Handlers always return `Result` / `Result<T>`** — the exception and validation behaviours silently
skip anything else. **Handlers must be `public`**: the mediator's generated code in the host references
them, so `internal` fails with `CS0122`.

**React to an Integration Event only through an inbox handler**, whose Command stays short and inside
this feature's data — it holds the inbox transaction open (skill: `reaching-another-feature`).

`Web` reaches `Application` only by sending a message — never by calling a handler directly.
Blazor sends it with `IScopedMediator` (below); anything else uses `IMediator`.
Validators check shape, never business rules, and never inject a repository. **A command sent only
from a Blazor form gets no validator** (skills: `adding-a-blazor-form`, `adding-a-command-or-query`).

### Dependency injection

Register a service in the owning layer's `Configuration.cs` — not in `Program.cs`, and not in another
feature's composition root.

### Blazor components

**Load the `dotnet-blazor` skills before writing or changing a `.razor` file**: `plan-ui-change`
for a page with several regions, `author-component` for any component, `collect-user-input` beside
`adding-a-blazor-form` for a form. They decide which component owns state and sends messages; the
sections below only add this project's specifics.

### Database access from Blazor components

Scoped services live for the whole SignalR circuit, so an injected `IMediator` shares one long-lived,
non-thread-safe `DbContext` for the user's session. **Never `@inject` `IMediator` for database work**:
inject `IScopedMediator`, which gives each message a scope of its own. That scope is gone before the
component renders, so handlers return DTOs, never entities.

### Render mode

**Interactivity is global with prerendering off** (`App.razor`), so `OnInitializedAsync` runs once.
Don't add `@rendermode` to pages or components, and don't read `HttpContext` in a component — in a
circuit it is null, silently. A page that needs it, or must render without a circuit, gets
`[ExcludeFromInteractiveRouting]`, as `Error` and `NotFound` do.

## Conventions

- **Tests**: xUnit v3 on Microsoft.Testing.Platform, **NSubstitute** for substitutes, **bUnit** for
  Blazor component tests.
- **Comments: keep them short.** An XML `<summary>` is a line or two. A `<remarks>` or an inline comment earns its space only by recording what the code cannot say. Two tight lines beat a well-written paragraph; if a comment runs past a few lines, cut it rather than polishing it.

## MCP servers

- **mslearn** — look up current .NET / C# APIs. This solution targets the latest .NET, so avoid
  writing outdated C#.

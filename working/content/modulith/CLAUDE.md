# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

TODO: project description

## Commands

```bash
# Fetch nuget packages
dotnet restore
# Then the following commands can all use --no-restore for faster execution

# Build (CI uses -warnaserror, so treat warnings as errors locally too)
dotnet build --no-restore -warnaserror

# Run the web app (needs the Postgres db container from the devcontainer)
cd src/ModulithTemplate.Web && dotnet run

# Test — runner is Microsoft.Testing.Platform (configured in global.json), not VSTest

# all tests
dotnet test --no-restore

# one project
dotnet test --no-restore --project test/Features/Orders/ModulithTemplate.Features.Orders.DomainTests
# one class
dotnet test --no-restore --project <project> --filter-class "*SomeEntityTests*"
# one method
dotnet test --no-restore --project <project> --filter-method "*rejects_a_negative_quantity*"
```

### EF Core migrations

Each feature owns its own `DbContext`, schema, and migrations in its `*.Infrastructure` project.
Because the host registers more than one `DbContext`, **`--context` is always required**.
Two scripts at the solution root wrap this — prefer them:

```bash
bash add-migration.sh Orders InitialOrders  # <FeatureName> <MigrationName>
bash update-database.sh                     # applies every context's pending migrations
```

The devcontainer provides Postgres 18 (`localhost:5432`, user/pass/db all `postgres`).

CI discovers contexts from the host's DI container, so a new feature is covered as soon as
`Program.cs` calls its `ConfigureXxxFeature()`. `build.yml` fails the build on a model change
that landed without its migration; `release-migrations.yml` attaches one idempotent
`<Context>.sql` per schema to a published release.

## Architecture

A vertical-slice modular monolith built on DDD. Each feature under `src/Features/<Name>/` owns
four layer projects:

```
Web  →  Application  →  Domain  ←  Infrastructure
```

- **Domain** has no outbound dependencies. It is the only place business rules live.
- **Application** orchestrates the domain through commands and queries.
- **Infrastructure** implements persistence for the domain's abstractions.
- **Web** is the feature's composition root and its only UI/endpoint surface.

Features must not depend on each other. `ModulithTemplate.ArchitectureTests` enforces the layer
rules, the cross-feature isolation, and the naming/placement conventions below — each in both
directions, so a `*Spec` outside `Specifications/` fails just as a badly-named type inside it does.

Each feature's four layers are mirrored by four test projects under `test/Features/<Name>/`.
`src/` holds production code only.

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
| HTTP, Blazor, `DbContext`, JSON, configuration | `Web` / `Infrastructure` |

Signals you have put a rule in the wrong place:

- A handler decides whether something is *valid*, rather than reacting to the domain's answer.
- The same rule is written more than once, e.g. on both the create and the update path. That
  duplication is the signal it belongs in the entity, where it is enforced once for all callers.
- An entity has public setters and no methods, while a handler mutates it property by property.
- A Blazor component enforces a rule that nothing else enforces.
- A mapper or DTO computes something a caller could be wrong about.
- An entity injects or calls a repository. Entities do not reach for data; a domain service does.

### Writing entities

Entities are rich, not data bags:

```csharp
public class Order
{
    private readonly List<OrderLine> _lines = [];

    private Order() { }                          // EF only

    internal Order(Guid customerId)              // internal: no `new` from Application
    {
        CustomerId = customerId;
        Status = OrderStatus.Draft;
    }

    /// <summary>Starts a new draft order for a customer.</summary>
    public static Order Place(Guid customerId) => new(customerId);

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;

    /// <summary>Adds a line, enforcing that only draft orders may be changed.</summary>
    public void AddLine(Guid productId, int quantity)
    {
        if (Status is not OrderStatus.Draft) throw new InvalidOperationException("Only a draft order can be changed.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        _lines.Add(new OrderLine(productId, quantity));
    }
}
```

The rules this encodes:

- **Private setters**, and collections exposed as `IReadOnlyList<T>` over a private backing list.
  State changes only through named methods that say what happened (`AddLine`, `Confirm`, `Cancel`).
- **`internal` constructor + private parameterless ctor for EF.** Application obtains instances
  through a `public static` factory method or a domain service — never `new`.
- **Invariants are enforced in the constructor and in every mutator**, so no call path can
  produce an invalid entity. The domain throws on violation; it does not return `Result`
  (`Domain` deliberately takes no dependency on FluentResults). Unhandled exceptions from a
  handler are converted into a failed `Result` centrally, so a violated invariant surfaces as a
  failure rather than a crash — but an *expected* failure should be checked by the handler and
  returned as an explicit `Result.Fail` with a meaningful message.
- The Orders feature's `SomeEntity` placeholder deliberately breaks these rules: it is an
  unmapped stand-in to delete, not a pattern to copy.

Cross-entity rules that need data access go in `Domain/Services/` as `*DomainService` classes,
which depend on the feature's repository interface, never on EF Core.

**When you add or change an invariant, cover it with a `<Name>.DomainTests` test on the entity**,
not only through a handler test — the domain is where the guarantee lives.

### Data access

Data access is abstracted behind a feature-owned `I<Name>Repository<T>` (e.g. `IOrdersRepository<T>`)
in `Domain`, extending the shared `IRepository<T>` from `ModulithTemplate.FeatureCore`, which
extends Ardalis.Specification's `IRepositoryBase<T>`.

Handlers and domain services inject the **per-feature** interface — never `IRepository<T>` directly.
The open-generic DI registration is keyed on the interface type, so several features registering
`IRepository<>` would leave the last one serving every feature's entities from the wrong `DbContext`.

Query logic belongs in `Domain/Specifications/` as `*Spec` classes, not inline in handlers.

### Application layer (CQRS)

Every operation is a `public sealed record` message (`ICommand<T>` / `IQuery<T>`) plus its
`public sealed` handler, in a folder of its own:

```
Application/Commands/AddSomeEntity/{AddSomeEntityCommand,AddSomeEntityCommandHandler}.cs
Application/Queries/GetSomeEntityCount/{GetSomeEntityCountQuery,GetSomeEntityCountQueryHandler}.cs
Application/Dtos/*Dto.cs
Application/Mappers/*Mapper.cs        // Mapperly [Mapper] partial classes
```

A handler orchestrates and nothing more: load entities via the repository, call entity methods or
a domain service, persist, map to a DTO. It holds the happy path plus explicit `Result.Fail` for
expected domain failures, and no `try`/`catch` — unhandled exceptions are converted centrally.

Two rules that are easy to get wrong:

- **Handlers always return `Result` / `Result<T>`.** The central exception-to-`Result` conversion
  is constrained to result responses and is silently skipped for a handler returning anything else.
- **Handlers must be `public`.** The mediator's source generator emits a hard `typeof(<Handler>)`
  reference into the host's compilation, so marking one `internal` breaks the host build with
  `CS0122`. Do not "tidy" them.

`Web` reaches `Application` only through `IMediator` — never by calling a handler directly.

### Infrastructure

EF Core + Npgsql, owned entirely by the feature: a concrete `DbContext` (schema set via
`HasDefaultSchema` in `OnModelCreating`), a context-bound
`<Name>Repository<T> : RepositoryBase<T>, I<Name>Repository<T>`, and its own `Data/Migrations/`.
DB naming is snake_case via `EFCore.NamingConventions`, applied through the shared
`ModulithTemplate.Infrastructure.Common` project's `AddModuleDbContext<TContext>(configuration, schema)`.
That shared project defines EF conventions only and never a concrete `DbContext`.

### Dependency injection

Each feature layer exposes a `Configuration.cs` with a
`ConfigureXxxYyy(this IServiceCollection[, IConfiguration]) : IServiceCollection` extension
(e.g. `ConfigureOrdersInfrastructure`, `ConfigureOrdersApplication`). The feature's `Web` project
exposes `ConfigureXxxFeature(this WebApplicationBuilder)`, which calls its own layers; `Program.cs`
calls each feature's `ConfigureXxxFeature` once.

Register a service in the owning layer's `Configuration.cs` — not in `Program.cs`, not in another
feature's composition root. **Handlers are the one exception**: the host's `AddMediator` discovers
them automatically, so they need no registration.

### Database access from Blazor components

Scoped services live for the whole SignalR circuit in Blazor Server, so a directly-injected
scoped dependency shares one long-lived, non-thread-safe `DbContext` for the entire user session.
`IMediator` is registered scoped too, so it is no exception.

**Components must not `@inject` `IMediator` for database work.** Inject `IServiceScopeFactory` and
send each message in a fresh scope:

```csharp
var result = await ScopeFactory.WithNewScopeAsync(sp =>
    sp.GetRequiredService<IMediator>().Send(new GetSomeEntityCountQuery(), CancellationToken.None));
```

`WithNewScopeAsync` lives in `ModulithTemplate.Web.Common/Extensions/ServiceScopeExtensions.cs`.
A component using it needs `@using Mediator` and `@using ModulithTemplate.Web.Common.Extensions` —
neither is in `_Imports.razor`. Stateless, non-DB services may stay directly injected.

## Conventions

- **Central management**: target framework, nullable and analyzers come from `Directory.Build.props`;
  all package versions are pinned in `Directory.Packages.props` (central package management — add new
  deps there, version-less `PackageReference` in the csproj).
- **Analyzers as gatekeepers**: Meziantou, SonarAnalyzer and Roslynator run on build with
  `EnforceCodeStyleInBuild`, and CI builds with `-warnaserror`. Suppress narrowly with
  `#pragma warning disable <id>` + matching restore when a rule genuinely doesn't apply, rather
  than disabling globally.
- **Tests**: xUnit v3 on Microsoft.Testing.Platform, **NSubstitute** for substitutes, **bUnit** for
  Blazor component tests. Shared settings and common test packages come from
  `test/Directory.Build.props`, so an individual test `.csproj` normally holds nothing but a
  `ProjectReference`.

## Code Style

- Prefer clear code over comments; use inline comments sparingly.
- C#: 4-space indent, `PascalCase` for classes/methods, `_camelCase` for private fields,
  `camelCase` for locals and parameters.
- Prefer primary constructors where possible; use auto-properties, and `field` if necessary.
- Write XML comments on all public classes, methods, properties and fields.
- Tests: `<ClassName>Tests` for the class, `<MethodName>_<Conditions>_<AssertedOutcome>` in
  snake_case for methods (never an `Async` suffix), and Arrange/Act/Assert with a comment per section.

## Adding a feature

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Payments
```

Run this from the solution root (the directory containing the `.slnx`). It creates the four layer
projects under `src/Features/Payments/` and the four matching test projects under
`test/Features/Payments/`, adds all eight to the solution, and wires up the full persistence and DI
scaffolding mirroring `Orders` — repository interface, `PaymentsContext` (schema `payments`),
repository implementation, empty `Data/Migrations/`, both `Configuration.cs` files, and
`PaymentsModule.ConfigurePaymentsFeature`.

**The one manual step** is registering the feature with the host — add the project reference and
call `builder.ConfigurePaymentsFeature();` in `Program.cs`:

```bash
dotnet add src/ModulithTemplate.Web/ModulithTemplate.Web.csproj reference \
  src/Features/Payments/ModulithTemplate.Features.Payments.Web/ModulithTemplate.Features.Payments.Web.csproj
```

Then model the feature's entities under `Payments.Domain/Entities/` and create its first migration.

## Versioning

Do not perform any git actions. I will do them myself.

## Model Context Protocol (MCP) Servers

### mslearn

Use the `mslearn` MCP server to look up current .NET / C# APIs and features. This solution targets
the latest .NET, so avoid writing outdated C#.

## Additional Tools

@.claude/RTK.md

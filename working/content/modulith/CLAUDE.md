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
dotnet build --no-restore
dotnet build --no-restore -warnaserror

# Run the web app (needs the Postgres db container from the devcontainer)
cd src/ModulithTemplate.Web && dotnet run

# Test — runner is Microsoft.Testing.Platform (configured in global.json), not VSTest

# all tests
dotnet test --no-restore

# one project
dotnet test --no-restore --project test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests

# one class
dotnet test --no-restore --project test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests --filter-class "*OrdersApplicationSmokeTests*"

# one method
dotnet test --no-restore --project test/Features/Orders/ModulithTemplate.Features.Orders.InfrastructureTests --filter-method "*OrdersContext_model_defaults_to_the_orders_schema*"
```

### EF Core migrations

Each feature owns its own `DbContext`, schema, and migrations in its `*.Infrastructure` project. Startup project is always `ModulithTemplate.Web`. Because the host registers more than one `DbContext`, **`--context` is required** — without it `dotnet ef` fails with "More than one DbContext was found".

Two scripts at the solution root wrap this for local development — prefer them over the raw commands:

```bash
bash add-migration.sh Orders InitialOrders  # <FeatureName> <MigrationName>, writes to the feature's Data/Migrations
bash update-database.sh                     # applies every discovered context's pending migrations
```

The raw equivalents:

```bash
# Create a new migration for a feature (Orders shown; repeat per feature)
dotnet ef migrations add "Name" -o Data/Migrations --context OrdersContext \
  --project src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj

# Apply a feature's pending migrations
dotnet ef database update --context OrdersContext \
  --project src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj
```

The devcontainer provides Postgres 18 (`localhost:5432`, user/pass/db all `postgres`); the app container shares the db container's network.

Both CI workflows discover the contexts from the host's DI container rather than a hard-coded list, so a new feature is picked up as soon as `Program.cs` calls its `ConfigureXxxFeature()`:

- `build.yml` runs `dotnet ef migrations has-pending-model-changes` per context, so a model change that lands without its migration fails the build. No database is needed — it compares the current model against the last migration's snapshot.
- `release-migrations.yml` runs `dotnet ef migrations script --idempotent` per context on a published release and attaches one `<Context>.sql` per schema to the release. Each script only touches its own schema and `__EFMigrationsHistory` table, so they can be applied in any order.

## Code Style

- General:
    - Prefer writing clear code and use inline comments sparingly
- C#: 
    - 4-space indent
    - `PascalCase` for classes/methods
    - `_camelCase` for private fields
    - `camelCase` for local variables, parameters
    - Prefer primary constructors where possible
    - Use auto-properties, and `field` if necessary
    - Write XML comments on all public classes, methods, properties and fields
    - Tests:
        - `<ClassName>Tests` for test class
        - `<MethodName>_<Conditions>_<AssertedOutcome>` for test methods (never `Async` suffix)
        - Arrange, Act, Assert pattern (comment each section in method)

## Versioning

Do not perform any git actions. I will do them myself.

## Architecture

This solution follows a vertical-slice modular monolith: each feature under `src/Features/<Name>/` owns four layer projects forming a dependency chain Web → Application → Domain ← Infrastructure (Domain has no outbound dependencies). Feature modules must not depend on each other (enforced by `ModulithTemplate.ArchitectureTests`, which also enforces the naming and placement conventions below — each in both directions, so a `*Spec` outside `Specifications/` fails just as a badly-named type inside it does). This follows the domain driven design (DDD) architecture principles. Each feature's four layer projects are mirrored by four test projects under `test/Features/<Name>/`, one per layer. `src/` holds production code only.

- **`<Name>.Domain`** — entities, domain services, and abstractions only. Use rich entities: private setters, `internal` constructors, a private parameterless ctor for EF, and invariants enforced in the ctor and mutator methods. Since the constructor is `internal`, a handler obtains an instance through a `public static` factory method on the entity, or via a domain service — never `new`; the Orders feature's `SomeEntity` placeholder instead has a public parameterless ctor, precisely because it is an unmapped stand-in that sidesteps this rule. Cross-entity rules that need data access live in domain services, as `*DomainService` classes under `Services/`. Data access is abstracted behind a **feature-owned** `I<Name>Repository<T>` (e.g. `IOrdersRepository<T>`) that extends the shared `IRepository<T>` from `ModulithTemplate.FeatureCore`, which in turn extends Ardalis.Specification's `IRepositoryBase<T>`. The per-feature interface is what handlers inject — never `IRepository<T>` directly, because the open-generic DI registration is keyed on the interface type, so several features registering `IRepository<>` would leave the last one registered serving every feature's entities from the wrong `DbContext`. Query logic lives in `Specifications/` as `*Spec` classes.
- **`<Name>.Application`** — orchestration layer, entered only through the mediator. Each operation is a `public sealed record` message (`ICommand<T>`/`IQuery<T>`) plus its `public sealed` handler, in a folder of its own: `Commands/AddSomeEntity/{AddSomeEntityCommand,AddSomeEntityCommandHandler}.cs`, `Queries/GetSomeEntityCount/{GetSomeEntityCountQuery,GetSomeEntityCountQueryHandler}.cs` (the Orders feature ships both as worked examples). Handlers load entities via repositories, invoke domain services, persist, and map to DTOs in `Dtos/`. They return **FluentResults** `Result`/`Result<T>` — always, because the exception behaviour is constrained to result responses and is silently omitted for a handler returning anything else; callers branch on `IsFailed`/`Errors`. Handlers hold the happy path plus explicit `Result.Fail` for expected domain failures; they carry no `try`/`catch`, since unhandled exceptions are converted centrally (see "CQRS and the mediator pipeline"). **Handlers are `public` because the mediator's source generator requires it**: it emits a hard `typeof(<Handler>)` reference for every handler it discovers into the host's compilation, so marking one `internal` breaks the host build with `CS0122` — do not "tidy" them. Going through `IMediator` rather than calling a handler directly is therefore a convention this codebase follows, not a guarantee the compiler enforces. Entity↔DTO mapping uses **Mapperly** source generators (`Mappers/*Mapper.cs`, `[Mapper]` partial classes).
- **`<Name>.Infrastructure`** — EF Core + Npgsql implementation, owned entirely by the feature: a concrete `DbContext` (schema set via `HasDefaultSchema` in `OnModelCreating`), a context-bound `<Name>Repository<T> : RepositoryBase<T>, I<Name>Repository<T>`, and its own `Data/Migrations/`. DB naming is snake_case via `EFCore.NamingConventions`, applied uniformly through the shared `ModulithTemplate.Infrastructure.Common` project's `AddModuleDbContext<TContext>(configuration, schema)` extension — that shared project defines EF conventions only and never a concrete `DbContext`.
- **`<Name>.Web`** — the feature's composition root, the only layer allowed to reference both `Application` and `Infrastructure`. Exposes a `ConfigureXxxFeature(this WebApplicationBuilder)` extension (e.g. `OrdersModule.ConfigureOrdersFeature`) that calls the feature's `ConfigureXxxInfrastructure`/`ConfigureXxxApplication` and registers its endpoints/Razor components.

`ModulithTemplate.Web` is the host / composition root: `Program.cs` calls each feature's `ConfigureXxxFeature()` once — see "Adding a feature" below.

### Where business rules live

Business rules and invariants belong in the entity (or a domain service when they span entities), never in handlers, components, or DTO mapping. A handler orchestrates — load, call domain methods, persist, map — it does not *decide* what a valid entity looks like. If the same rule can be violated through more than one call path (e.g. create and update), that is the signal it belongs in the entity, where it is enforced once for all callers.

When adding or changing an invariant, cover it with a `<Name>.DomainTests` test on the entity, not only via the handler test — the domain is where the guarantee now lives.

### CQRS and the mediator pipeline

`Web` talks to `Application` through [martinothamar/Mediator](https://github.com/martinothamar/Mediator) — never by calling a handler directly. The mediator's source generator is referenced only by `ModulithTemplate.Web` (feature projects reference `Mediator.Abstractions` only), so `ModulithTemplate.Web/Program.cs` holds the single `AddMediator` call for the whole solution:

```csharp
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(LoggingBehaviour<,>), typeof(ExceptionBehaviour<,>)];
});
```

`ServiceLifetime.Scoped` is required rather than stylistic: handlers inject the feature repositories, which are bound to a scoped `DbContext`.

**Handlers register themselves.** The generated `AddMediator` discovers every handler in the assemblies the host references, so a feature's handlers reach the container without an entry in that feature's `Configuration.cs`. This is the one documented exception to "register services in the owning layer's `Configuration.cs`" — everything that is not a handler still belongs there.

Two behaviours wrap every message. The generated code wraps the array in reverse, so the **first entry ends up outermost**:

- **`LoggingBehaviour`** — logs `Debug` on start and, on completion, `Information` with elapsed milliseconds, or `Warning` when the handler returned a failed `Result`. Being outermost, it observes a uniform `Result` outcome for handlers returning `Result`/`Result<T>`, because `ExceptionBehaviour` below it has already converted any exception. It is deliberately unconstrained on the response type, so every message is logged whatever its handler returns.
- **`ExceptionBehaviour`** — catches unhandled exceptions, logs them at `Error` with the stack trace, and converts them into a failed `Result` of the handler's own response type. `OperationCanceledException` is rethrown untouched: cancellation is not a failure. It is constrained `where TResponse : ResultBase<TResponse>, new()`, and the generator honours that constraint — a message whose handler returns something that is not a result silently gets no exception conversion at all. That is why every handler here returns `Result`/`Result<T>`.

Both are `internal sealed` and live in `ModulithTemplate.Web/Behaviours/` beside the registration that installs them, because the host is their only consumer; their `[LoggerMessage]` definitions sit next to them in `BehaviourLog.cs`.

### Dependency injection

Each feature layer exposes a `Configuration.cs` with a `ConfigureXxxYyy(this IServiceCollection[, IConfiguration]) : IServiceCollection` extension method (e.g. `ConfigureOrdersInfrastructure`, `ConfigureOrdersApplication`). The feature's `Web` project's `ConfigureXxxFeature` calls its own layers' `Configuration.cs` methods; `Program.cs` calls each feature's `ConfigureXxxFeature` once. When adding a service, register it in the owning layer's `Configuration.cs`, not in `Program.cs` or another feature's composition root.

### Database access from Blazor components

Scoped services (including each feature's `DbContext`) live for the whole SignalR circuit in Blazor Server, so a directly-injected scoped dependency would share one long-lived, non-thread-safe `DbContext` for the entire user session — causing concurrent-operation crashes, change-tracker bloat, and stale reads. `IMediator` is registered scoped too, so it is no exception. Therefore **components must not `@inject` `IMediator` (or any other scoped dependency) for database work**. Instead inject `IServiceScopeFactory` and send each message inside a fresh scope via the `WithNewScopeAsync` extension (`ModulithTemplate.Web.Common/Extensions/ServiceScopeExtensions.cs`, which has both `Task` and `ValueTask` overloads — `IMediator.Send` returns a `ValueTask`):

```csharp
var result = await ScopeFactory.WithNewScopeAsync(sp =>
    sp.GetRequiredService<IMediator>().Send(new GetSomeEntityCountQuery(), CancellationToken.None));
```

A component using this needs `@using Mediator` and `@using ModulithTemplate.Web.Common.Extensions` — neither is in `_Imports.razor`.

Stateless, non-DB services may stay directly injected.

## Conventions

- **Central management**: target framework, nullable, analyzers, and `<TargetFramework>net10.0</TargetFramework>` come from `Directory.Build.props`; all package versions are pinned in `Directory.Packages.props` (central package management — add new deps there, version-less `PackageReference` in the csproj).
- **Analyzers as gatekeepers**: Meziantou, SonarAnalyzer, and Roslynator run on build with `EnforceCodeStyleInBuild`. CI builds with `-warnaserror`. Suppress narrowly with `#pragma warning disable <id>` + matching restore when a rule genuinely doesn't apply (see existing EF-ctor and static-method suppressions), rather than disabling globally.
- **Tests**: xUnit v3 on Microsoft.Testing.Platform, **NSubstitute** for substitutes, **bUnit** for Blazor component tests. Each feature owns four test projects mirroring its layers at `test/Features/<Name>/<App>.Features.<Name>.{Domain,Application,Infrastructure,Web}Tests`, scaffolded together with the feature by `dotnet new modulith-feature`. Shared settings and the common test packages (xUnit, NSubstitute, coverage) come from `test/Directory.Build.props`, so an individual test `.csproj` normally holds nothing but a `ProjectReference`. Test method names are snake_case describing behavior.
- **CQRS building blocks**: commands and their handlers live under `Application/Commands/<Operation>/` and are named `*Command` / `*CommandHandler`; queries under `Application/Queries/<Operation>/` as `*Query` / `*QueryHandler`; DTOs in `Application/Dtos/` as `*Dto`. `ModulithTemplate.ArchitectureTests` enforces all three in both directions, so a `*Dto` outside `Dtos/` fails just as a badly-named type inside it does.

## Docs

Design spec and implementation plan live in `docs/superpowers/specs/` and `docs/superpowers/plans/`.

## Adding a feature

Scaffold a new feature's four layer projects and register them in the solution:

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Payments
```

Run this from the solution root (the directory containing the `.slnx`). It creates
`src/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}`
plus the matching `test/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}Tests`,
and adds all eight to the solution under `/src/Features/Payments/` and `/test/Features/Payments/` folders.

The scaffold already contains the full persistence and DI wiring, mirroring `Orders`:

- `Payments.Domain` — `IPaymentsRepository<T> : IRepository<T>`, the feature's repository abstraction.
- `Payments.Infrastructure` — `Data/PaymentsContext.cs` (schema `payments`), `Data/PaymentsRepository.cs`, an empty `Data/Migrations/`, and a `Configuration.cs` whose `ConfigurePaymentsInfrastructure` calls `AddModuleDbContext<PaymentsContext>(configuration, schema: "payments")` and registers `IPaymentsRepository<>`.
- `Payments.Application` — a `Configuration.cs` with an empty `ConfigurePaymentsApplication` to register non-handler services into, plus empty `Commands/`, `Queries/` and `Dtos/` folders. Handlers need no registration: the host's `AddMediator` discovers them.
- `Payments.Web` — `PaymentsModule.ConfigurePaymentsFeature(this WebApplicationBuilder)`, calling both of the above.
- `test/Features/Payments/…{Domain,Application,Infrastructure,Web}Tests` — one test project per layer, each with a single smoke test to replace with real ones.

**The one manual step** is registering the feature with the host: in `ModulithTemplate.Web`, add a `ProjectReference` to `Payments.Web` and call `builder.ConfigurePaymentsFeature();` in `Program.cs`.

```bash
dotnet add src/ModulithTemplate.Web/ModulithTemplate.Web.csproj reference \
  src/Features/Payments/ModulithTemplate.Features.Payments.Web/ModulithTemplate.Features.Payments.Web.csproj
```

Then add the feature's entities under `Payments.Domain/Entities/` and create its first migration (see "EF Core migrations" above — pass `--context PaymentsContext`).

## Model Context Protocol (MCP) Servers

### mslearn

Use the `mslearn` MCP server to find information about latest dotnet / C# features when implementing new features, since we are using the latest dotnet version we should not write old/outdated C# code.

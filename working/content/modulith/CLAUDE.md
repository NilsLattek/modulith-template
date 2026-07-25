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
dotnet test --no-restore                                                  # all tests
dotnet test --no-restore tests/ModulithTemplate.ApplicationTests          # one project
dotnet test --no-restore --filter-class "*SomwAppServiceTests*"           # one class
dotnet test --no-restore --filter-method "*AddAsync_creates_entity*"      # one method
```

### EF Core migrations

Each feature owns its own `DbContext`, schema, and migrations in its `*.Infrastructure` project. Startup project is always `ModulithTemplate.Web`. Because the host registers more than one `DbContext`, **`--context` is required** — without it `dotnet ef` fails with "More than one DbContext was found".

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

This solution follows a vertical-slice modular monolith: each feature under `src/Features/<Name>/` owns four layer projects forming a dependency chain Web → Application → Domain ← Infrastructure (Domain has no outbound dependencies). Feature modules must not depend on each other (enforced by `ModulithTemplate.ArchitectureTests`). This follows the domain driven design (DDD) architecture principles.

- **`<Name>.Domain`** — entities, domain services, and abstractions only. Use rich entities: private setters, `internal` constructors, a private parameterless ctor for EF, and invariants enforced in the ctor and mutator methods. Cross-entity rules that need data access live in domain services (`*DomainService`). Data access is abstracted behind a **feature-owned** `I<Name>Repository<T>` (e.g. `IOrdersRepository<T>`) that extends the shared `IRepository<T>` from `ModulithTemplate.FeatureCore`, which in turn extends Ardalis.Specification's `IRepositoryBase<T>`. The per-feature interface is what app services inject — never `IRepository<T>` directly, because the open-generic DI registration is keyed on the interface type, so several features registering `IRepository<>` would leave the last one registered serving every feature's entities from the wrong `DbContext`. Query logic lives in `Specifications/` as `*Spec` classes.
- **`<Name>.Application`** — orchestration layer. App services (`I*AppService`, `internal` impls) load entities via repositories, invoke domain services, persist, and map to DTOs. They return **FluentResults** `Result`/`Result<T>` — exceptions (except `OperationCanceledException`) are caught and turned into `Result.Fail`; callers branch on `IsFailed`/`Errors`. Entity↔DTO mapping uses **Mapperly** source generators (`Mapper/*Mapper.cs`, `[Mapper]` partial classes).
- **`<Name>.Infrastructure`** — EF Core + Npgsql implementation, owned entirely by the feature: a concrete `DbContext` (schema set via `HasDefaultSchema` in `OnModelCreating`), a context-bound `<Name>Repository<T> : RepositoryBase<T>, I<Name>Repository<T>`, and its own `Data/Migrations/`. DB naming is snake_case via `EFCore.NamingConventions`, applied uniformly through the shared `ModulithTemplate.Infrastructure.Common` project's `AddModuleDbContext<TContext>(configuration, schema)` extension — that shared project defines EF conventions only and never a concrete `DbContext`.
- **`<Name>.Web`** — the feature's composition root, the only layer allowed to reference both `Application` and `Infrastructure`. Exposes a `ConfigureXxxFeature(this WebApplicationBuilder)` extension (e.g. `OrdersModule.ConfigureOrdersFeature`) that calls the feature's `ConfigureXxxInfrastructure`/`ConfigureXxxApplication` and registers its endpoints/Razor components.

`ModulithTemplate.Web` is the host / composition root: `Program.cs` calls each feature's `ConfigureXxxFeature()` once — see "Adding a feature" below.

### Where business rules live

Business rules and invariants belong in the entity (or a domain service when they span entities), never in app services, components, or DTO mapping. An app service orchestrates — load, call domain methods, persist, map — it does not *decide* what a valid entity looks like. If the same rule can be violated through more than one call path (e.g. create and update), that is the signal it belongs in the entity, where it is enforced once for all callers.

When adding or changing an invariant, cover it with a `<Name>.DomainTest` test on the entity, not only via the app-service test — the domain is where the guarantee now lives.

### Dependency injection

Each feature layer exposes a `Configuration.cs` with a `ConfigureXxxYyy(this IServiceCollection[, IConfiguration]) : IServiceCollection` extension method (e.g. `ConfigureOrdersInfrastructure`, `ConfigureOrdersApplication`). The feature's `Web` project's `ConfigureXxxFeature` calls its own layers' `Configuration.cs` methods; `Program.cs` calls each feature's `ConfigureXxxFeature` once. When adding a service, register it in the owning layer's `Configuration.cs`, not in `Program.cs` or another feature's composition root.

### Database access from Blazor components

Scoped services (including `ModulithTemplateDbContext`) live for the whole SignalR circuit in Blazor Server, so a directly-injected app service would share one long-lived, non-thread-safe `DbContext` for the entire user session — causing concurrent-operation crashes, change-tracker bloat, and stale reads. Therefore **components must not `@inject` the app services** directly. Instead inject `IServiceScopeFactory` and run each DB-touching call inside a fresh scope via the `WithNewScopeAsync` extension (`ModulithTemplate.Web/Extensions/ServiceScopeExtensions.cs`):

```csharp
var result = await ScopeFactory.WithNewScopeAsync(sp =>
    sp.GetRequiredService<ISomeAppService>().UpdateAsync(id, dto, CancellationToken.None));
```

Stateless, non-DB services may stay directly injected.

## Conventions

- **Central management**: target framework, nullable, analyzers, and `<TargetFramework>net10.0</TargetFramework>` come from `Directory.Build.props`; all package versions are pinned in `Directory.Packages.props` (central package management — add new deps there, version-less `PackageReference` in the csproj).
- **Analyzers as gatekeepers**: Meziantou, SonarAnalyzer, and Roslynator run on build with `EnforceCodeStyleInBuild`. CI builds with `-warnaserror`. Suppress narrowly with `#pragma warning disable <id>` + matching restore when a rule genuinely doesn't apply (see existing EF-ctor and static-method suppressions), rather than disabling globally.
- **Tests**: xUnit v3, **bUnit** for Blazor component tests (`ModulithTemplate.WebTest`), **NSubstitute** for substitutes. Test method names are snake_case describing behavior. `ModulithTemplate.ApplicationTest` uses substitutes — see `TestFactory.CreateUnitOfWorkSubstitute` for the unit-of-work pattern in tests.

## Docs

Design spec and implementation plan live in `docs/superpowers/specs/` and `docs/superpowers/plans/`.

## Adding a feature

Scaffold a new feature's four layer projects and register them in the solution:

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Payments
```

Run this from the solution root (the directory containing the `.slnx`). It creates
`src/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}`
and adds all four to the solution under a `/src/Features/Payments/` folder.

The scaffold already contains the full persistence and DI wiring, mirroring `Orders`:

- `Payments.Domain` — `IPaymentsRepository<T> : IRepository<T>`, the feature's repository abstraction.
- `Payments.Infrastructure` — `Data/PaymentsContext.cs` (schema `payments`), `Data/PaymentsRepository.cs`, an empty `Data/Migrations/`, and a `Configuration.cs` whose `ConfigurePaymentsInfrastructure` calls `AddModuleDbContext<PaymentsContext>(configuration, schema: "payments")` and registers `IPaymentsRepository<>`.
- `Payments.Application` — a `Configuration.cs` with an empty `ConfigurePaymentsApplication` to register app services into.
- `Payments.Web` — `PaymentsModule.ConfigurePaymentsFeature(this WebApplicationBuilder)`, calling both of the above.

**The one manual step** is registering the feature with the host: in `ModulithTemplate.Web`, add a `ProjectReference` to `Payments.Web` and call `builder.ConfigurePaymentsFeature();` in `Program.cs`.

```bash
dotnet add src/ModulithTemplate.Web/ModulithTemplate.Web.csproj reference \
  src/Features/Payments/ModulithTemplate.Features.Payments.Web/ModulithTemplate.Features.Payments.Web.csproj
```

Then add the feature's entities under `Payments.Domain/Entities/` and create its first migration (see "EF Core migrations" above — pass `--context PaymentsContext`).

## Model Context Protocol (MCP) Servers

### mslearn

Use the `mslearn` MCP server to find information about latest dotnet / C# features when implementing new features, since we are using the latest dotnet version we should not write old/outdated C# code.

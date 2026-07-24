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

Startup project is always `ModulithTemplate.Web`; migrations live in `ModulithTemplate.Infrastructure`.

```bash
# Create a new migration
dotnet ef migrations add "Name" -o Data/Migrations \
  --project src/ModulithTemplate.Infrastructure/ModulithTemplate.Infrastructure.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj

# Apply pending migrations
dotnet ef database update \
  --project src/ModulithTemplate.Infrastructure/ModulithTemplate.Infrastructure.csproj \
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

This solution follows the domain driven design (DDD) architecture principles.

Four projects forming a dependency chain Web → Application → Domain ← Infrastructure (Domain has no outbound dependencies):

- **ModulithTemplate.Domain** — entities, domain services, and abstractions only. Use rich entities: private setters, `internal` constructors, a private parameterless ctor for EF, and invariants enforced in the ctor and mutator methods. Cross-entity rules that need data access live in domain services (`*DomainService`). Data access is abstracted behind `IRepository<T>` (extends Ardalis.Specification's `IRepositoryBase<T>`) and `IUnitOfWork`; query logic lives in `Specifications/` as `*Spec` classes.
- **ModulithTemplate.Application** — orchestration layer. App services (`I*AppService`, `internal` impls) load entities via repositories, invoke domain services, persist, and map to DTOs. They return **FluentResults** `Result`/`Result<T>` — exceptions (except `OperationCanceledException`) are caught and turned into `Result.Fail`; callers branch on `IsFailed`/`Errors`. Entity↔DTO mapping uses **Mapperly** source generators (`Mapper/*Mapper.cs`, `[Mapper]` partial classes).
- **ModulithTemplate.Infrastructure** — EF Core + Npgsql implementations: `ModulithTemplateDbContext`, `EfRepository<T>` (over Ardalis.Specification), `EfUnitOfWork` (wraps work in a transaction). Schema and relationships are configured in `ModulithTemplateDbContext.OnModelCreating`; DB naming is snake_case via `EFCore.NamingConventions`.
- **ModulithTemplate.Web** — Blazor Server (interactive server render mode). Presentation layer.

### Where business rules live

Business rules and invariants belong in the entity (or a domain service when they span entities), never in app services, components, or DTO mapping. An app service orchestrates — load, call domain methods, persist, map — it does not *decide* what a valid entity looks like. If the same rule can be violated through more than one call path (e.g. create and update), that is the signal it belongs in the entity, where it is enforced once for all callers.

When adding or changing an invariant, cover it with a `ModulithTemplate.DomainTest` test on the entity, not only via the app-service test — the domain is where the guarantee now lives.

### Dependency injection

Each non-domain project exposes a `Configuration.cs` with a `ConfigureXxx(this IServiceCollection, IConfiguration)` extension method. `Program.cs` calls `ConfigureInfrastructure` / `ConfigureDomain` / `ConfigureApplication` in order. When adding a service, register it in the owning project's `Configuration.cs`, not in `Program.cs`.

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

> **TODO:** the command does not wire the new feature's services into `Program.cs` /
> `Infrastructure` — there is no host module-registration convention yet. Follow the
> "Dependency injection" section above and register the feature's `Configuration.cs`
> manually until that convention exists.

## Model Context Protocol (MCP) Servers

### mslearn

Use the `mslearn` MCP server to find information about latest dotnet / C# features when implementing new features, since we are using the latest dotnet version we should not write old/outdated C# code.

# Feature-owned DbContext Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the shared, concrete `ModulithTemplate.Infrastructure` project (hardcoded `CatalogContext`/`EfRepository<T>`) with a per-feature `OrdersContext`/`OrdersRepository<T>` owned by `Features/Orders/*.Infrastructure`, plus a new `ModulithTemplate.Infrastructure.Common` project that holds only shared EF conventions (naming, migrations-history table, exception mapping) and no concrete `DbContext`.

**Architecture:** Implements `docs/superpowers/specs/2026-07-20-feature-owned-dbcontext-design.md` verbatim. Each feature gets its own `DbContext`, schema, migrations, and a `ConfigureXxxFeature` composition root in its `*.Web` project; the host `ModulithTemplate.Web/Program.cs` wires features in with one explicit call per feature (no marker interface, no assembly scanning).

**Tech Stack:** EF Core 10 / Npgsql, `EFCore.NamingConventions` (snake_case), `EntityFrameworkCore.Exceptions.PostgreSQL` (exception mapping), Ardalis.Specification (`RepositoryBase<T>`).

## Global Constraints

- All new identifiers keep the `ModulithTemplate` prefix (the `sourceName` token) so `dotnet new modulith` renames them correctly — never introduce a type/namespace that should be per-project without that prefix.
- Central Package Management is in force: add `<PackageReference Include="X" />` with **no version** in any `.csproj`; every package used here (`EFCore.NamingConventions`, `EntityFrameworkCore.Exceptions.PostgreSQL`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Ardalis.Specification.EntityFrameworkCore`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.EntityFrameworkCore.Design`) is already version-pinned in `Directory.Packages.props` — do not add new `PackageVersion` entries.
- Do not perform any git actions (commits, branches, etc.) — no task in this plan ends with a commit step. The maintainer commits when the work is reviewed (repo root `CLAUDE.md`).
- Build verification command (run from `working/content/modulith/`, the template content root — **not** the repo root):
  `dotnet build ModulithTemplate.slnx -warnaserror`
- Test verification command (same directory):
  `dotnet test ModulithTemplate.slnx`
- `EntityFramework.Exceptions.PostgreSQL`'s extension method lives in namespace `EntityFramework.Exceptions.PostgreSQL` (singular "EntityFramework", not "EntityFrameworkCore" — verified by loading the compiled package DLL), class `ExceptionProcessorExtensions`, method `UseExceptionProcessor(this DbContextOptionsBuilder)`. Get the `using` directive exactly right or the build fails with CS1061.
- `WebApplicationBuilder` / ASP.NET Core hosting types are not available in a plain `Microsoft.NET.Sdk` class library without `<FrameworkReference Include="Microsoft.AspNetCore.App" />` — every `*.Web` feature project that references `WebApplicationBuilder` needs that framework reference.

---

## Current state (verified before writing this plan)

- `src/ModulithTemplate.Infrastructure` (`CatalogContext`, `EfRepository<T>`, `Dependencies.ConfigureServices`) is **dead code** — no `.csproj` in the solution references it, and `Dependencies.ConfigureServices` is never called from `Program.cs`. It is safe to delete outright.
- `src/Features/Orders/*` is scaffold output from the feature sub-template: every layer contains a single empty placeholder class (`SomeEntity`, `Class1`, `ClassInfra`, `Class1`). `Orders.Domain` has zero project references. `Orders.Infrastructure` and `Orders.Application` each reference only `Orders.Domain`. `Orders.Web` references `Web.Common` and `Orders.Application` — **not** `Orders.Infrastructure`.
- `src/ModulithTemplate.Web/Program.cs` does not reference any feature project today.
- `src/ModulithTemplate.Web/appsettings.Development.json` has no `ConnectionStrings` section — needed once a real `AddDbContext` call executes at startup.
- No project in the solution references `Microsoft.EntityFrameworkCore.Design` (required by the `dotnet ef` CLI against the startup project); the `PackageVersion` already exists in `Directory.Packages.props` but is unused.
- `test/ModulithTemplate.ArchitectureTests` auto-references every project matching `src/Features/**/*.csproj`; it does **not** reference `ModulithTemplate.Infrastructure` or `ModulithTemplate.Infrastructure.Common` (neither has a `.Features.` path segment), so neither shared project is subject to `FeatureLayerTests`/`FeatureModuleTests`.

---

### Task 1: Shared `ModulithTemplate.Infrastructure.Common` project

**Files:**
- Create: `working/content/modulith/src/ModulithTemplate.Infrastructure.Common/ModulithTemplate.Infrastructure.Common.csproj`
- Create: `working/content/modulith/src/ModulithTemplate.Infrastructure.Common/ModuleDbContextExtensions.cs`
- Modify: `working/content/modulith/ModulithTemplate.slnx`

**Interfaces:**
- Produces: `ModulithTemplate.Infrastructure.Common.ModuleDbContextExtensions.AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string schema) : IServiceCollection where TContext : DbContext` — every feature `Configuration.cs` in later tasks calls this.

- [ ] **Step 1: Create the project file**

`working/content/modulith/src/ModulithTemplate.Infrastructure.Common/ModulithTemplate.Infrastructure.Common.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="EntityFrameworkCore.Exceptions.PostgreSQL" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

</Project>
```

No `ProjectReference` to `ModulithTemplate.FeatureCore` — this project is pure EF Core wiring, it has no notion of `IRepository<T>` or entities.

- [ ] **Step 2: Write the extension method**

`working/content/modulith/src/ModulithTemplate.Infrastructure.Common/ModuleDbContextExtensions.cs`:

```csharp
using EntityFramework.Exceptions.PostgreSQL;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.Infrastructure.Common;

public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : DbContext =>
        services.AddDbContext<TContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("PostgresConnection"),
                pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseSnakeCaseNamingConvention()
            .UseExceptionProcessor());
}
```

- [ ] **Step 3: Register the project in the solution**

In `working/content/modulith/ModulithTemplate.slnx`, inside the `<Folder Name="/src/">` block, add a new `<Project Path="..." />` line. The folder should read:

```xml
  <Folder Name="/src/">
    <Project Path="src/ModulithTemplate.FeatureCore/ModulithTemplate.FeatureCore.csproj" />
    <Project Path="src/ModulithTemplate.Infrastructure.Common/ModulithTemplate.Infrastructure.Common.csproj" />
    <Project Path="src/ModulithTemplate.Web.Common/ModulithTemplate.Web.Common.csproj" />
    <Project Path="src/ModulithTemplate.Web/ModulithTemplate.Web.csproj" />
  </Folder>
```

(Leave the old `ModulithTemplate.Infrastructure` line in place for now — Task 2 removes it. Insert the new line in the alphabetical spot shown above.)

- [ ] **Step 4: Verify it builds**

Run: `cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror`
Expected: build succeeds (the old `ModulithTemplate.Infrastructure` project is still present and unreferenced, so it still builds too).

---

### Task 2: Remove the old `ModulithTemplate.Infrastructure` project

**Files:**
- Delete: `working/content/modulith/src/ModulithTemplate.Infrastructure/` (entire directory: `Data/CatalogContext.cs`, `Data/EfRepository.cs`, `Data/Migrations/.gitkeep`, `Dependencies.cs`, `ModulithTemplate.Infrastructure.csproj`)
- Modify: `working/content/modulith/ModulithTemplate.slnx`

**Interfaces:**
- Consumes: nothing (verified in "Current state" above — no `.csproj` references this project, `Dependencies.ConfigureServices` is never called).
- Produces: nothing — this is a pure removal.

- [ ] **Step 1: Delete the project directory**

Run: `rm -rf working/content/modulith/src/ModulithTemplate.Infrastructure`

- [ ] **Step 2: Remove its entry from the solution**

In `working/content/modulith/ModulithTemplate.slnx`, delete this line from the `<Folder Name="/src/">` block:

```xml
    <Project Path="src/ModulithTemplate.Infrastructure/ModulithTemplate.Infrastructure.csproj" />
```

- [ ] **Step 3: Verify the solution still builds**

Run: `cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror`
Expected: build succeeds — nothing referenced the deleted project.

- [ ] **Step 4: Verify nothing else references the deleted project**

Run: `grep -rln "ModulithTemplate\.Infrastructure\." --include="*.csproj" --include="*.cs" --include="*.slnx" working/content/modulith | grep -v "Infrastructure.Common"`
Expected: no output (empty).

---

### Task 3: `Orders.Infrastructure` — `OrdersContext`, `OrdersRepository<T>`, `Configuration.cs`

**Files:**
- Create: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Data/OrdersContext.cs`
- Create: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Data/OrdersRepository.cs`
- Create: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Configuration.cs`
- Create: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Data/Migrations/.gitkeep` (empty file)
- Delete: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ClassInfra.cs`
- Modify: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj`

**Interfaces:**
- Consumes: `ModulithTemplate.Infrastructure.Common.ModuleDbContextExtensions.AddModuleDbContext<TContext>` (Task 1). `ModulithTemplate.FeatureCore.IRepository<T>` (already exists at `src/ModulithTemplate.FeatureCore/IRepository.cs`).
- Produces: `ModulithTemplate.Features.Orders.Infrastructure.Data.OrdersContext` (public, ctor `OrdersContext(DbContextOptions<OrdersContext> options)`), `ModulithTemplate.Features.Orders.Infrastructure.Data.OrdersRepository<T>` (internal), `ModulithTemplate.Features.Orders.Infrastructure.Configuration.ConfigureOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration) : IServiceCollection` — consumed by `OrdersModule` in Task 5.

- [ ] **Step 1: Update the csproj — package and project references**

Replace the contents of `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Ardalis.Specification.EntityFrameworkCore" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../ModulithTemplate.Features.Orders.Domain/ModulithTemplate.Features.Orders.Domain.csproj" />
    <ProjectReference Include="../../../ModulithTemplate.FeatureCore/ModulithTemplate.FeatureCore.csproj" />
    <ProjectReference Include="../../../ModulithTemplate.Infrastructure.Common/ModulithTemplate.Infrastructure.Common.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="Data/Migrations/" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Delete the placeholder class**

Run: `rm working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ClassInfra.cs`

- [ ] **Step 3: Create the migrations folder placeholder**

Run: `mkdir -p working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Data/Migrations && touch working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Data/Migrations/.gitkeep`

- [ ] **Step 4: Write `OrdersContext`**

`working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Data/OrdersContext.cs`:

```csharp
using System.Reflection;

using Microsoft.EntityFrameworkCore;

namespace ModulithTemplate.Features.Orders.Infrastructure.Data;

public class OrdersContext(DbContextOptions<OrdersContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("orders");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
```

- [ ] **Step 5: Write `OrdersRepository<T>`**

`working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Data/OrdersRepository.cs`:

```csharp
using Ardalis.Specification.EntityFrameworkCore;

using ModulithTemplate.FeatureCore;

namespace ModulithTemplate.Features.Orders.Infrastructure.Data;

internal sealed class OrdersRepository<T>(OrdersContext dbContext)
    : RepositoryBase<T>(dbContext), IRepository<T> where T : class;
```

- [ ] **Step 6: Write `Configuration.cs`**

`working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/Configuration.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.FeatureCore;
using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.Infrastructure.Common;

namespace ModulithTemplate.Features.Orders.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigureOrdersInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrdersContext>(configuration, schema: "orders");
        services.AddScoped(typeof(IRepository<>), typeof(OrdersRepository<>));
        return services;
    }
}
```

- [ ] **Step 7: Verify the solution builds**

Run: `cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror`
Expected: build succeeds. `Orders.Infrastructure` now compiles against `OrdersContext`/`OrdersRepository<T>`/`Configuration`.

- [ ] **Step 8: Verify the architecture tests still pass**

Run: `cd working/content/modulith && dotnet test ModulithTemplate.slnx`
Expected: `FeatureLayerTests` and `FeatureModuleTests` pass — `Orders.Infrastructure` depends only on `Orders.Domain` among feature layers (its other new references, `FeatureCore` and `Infrastructure.Common`, have no `.Features.` path segment so the layering rule does not see them).

---

### Task 4: `Orders.Application` — `Configuration.cs`

**Files:**
- Create: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/Configuration.cs`

**Interfaces:**
- Produces: `ModulithTemplate.Features.Orders.Application.Configuration.ConfigureOrdersApplication(this IServiceCollection services) : IServiceCollection` — consumed by `OrdersModule` in Task 5.

- [ ] **Step 1: Write `Configuration.cs`**

`working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/Configuration.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ModulithTemplate.Features.Orders.Application;

public static class Configuration
{
    public static IServiceCollection ConfigureOrdersApplication(this IServiceCollection services) => services;
}
```

No app services exist yet (the project only has the scaffolded `Class1` placeholder) — this is intentionally a no-op that establishes the convention every later app service registration will extend.

- [ ] **Step 2: Verify the solution builds**

Run: `cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror`
Expected: build succeeds.

---

### Task 5: `Orders.Web` — `OrdersModule.ConfigureOrdersFeature`

**Files:**
- Create: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Web/OrdersModule.cs`
- Modify: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Web/ModulithTemplate.Features.Orders.Web.csproj`

**Interfaces:**
- Consumes: `ConfigureOrdersInfrastructure` (Task 3), `ConfigureOrdersApplication` (Task 4).
- Produces: `ModulithTemplate.Features.Orders.Web.OrdersModule.ConfigureOrdersFeature(this WebApplicationBuilder builder) : WebApplicationBuilder` — consumed by the host `Program.cs` in Task 6.

- [ ] **Step 1: Update the csproj — framework reference and project references**

Replace the contents of `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Web/ModulithTemplate.Features.Orders.Web.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../../ModulithTemplate.Web.Common/ModulithTemplate.Web.Common.csproj" />
    <ProjectReference Include="../ModulithTemplate.Features.Orders.Application/ModulithTemplate.Features.Orders.Application.csproj" />
    <ProjectReference Include="../ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write `OrdersModule`**

`working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Web/OrdersModule.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Infrastructure;

namespace ModulithTemplate.Features.Orders.Web;

public static class OrdersModule
{
    public static WebApplicationBuilder ConfigureOrdersFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureOrdersInfrastructure(builder.Configuration);
        builder.Services.ConfigureOrdersApplication();
        return builder;
    }
}
```

- [ ] **Step 3: Verify the solution builds**

Run: `cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror`
Expected: build succeeds. This is the first project in the solution to use ASP.NET Core hosting types from a plain `Microsoft.NET.Sdk` project — if it fails with `WebApplicationBuilder` / `WebApplication` not found, confirm the `FrameworkReference` from Step 1 landed.

---

### Task 6: Host wiring — `Program.cs`, project reference, connection string, `dotnet ef` support

**Files:**
- Modify: `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/Program.cs`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/appsettings.Development.json`

**Interfaces:**
- Consumes: `OrdersModule.ConfigureOrdersFeature` (Task 5).

- [ ] **Step 1: Add the project reference and the EF Core Design package**

Replace the contents of `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../ModulithTemplate.Web.Common/ModulithTemplate.Web.Common.csproj" />
    <ProjectReference Include="../Features/Orders/ModulithTemplate.Features.Orders.Web/ModulithTemplate.Features.Orders.Web.csproj" />
  </ItemGroup>

</Project>
```

`Microsoft.EntityFrameworkCore.Design` is what makes the `dotnet ef migrations` / `dotnet ef database update` commands documented in `CLAUDE.md` (Task 7) work against this project as the `-s` startup project — it was never referenced anywhere in the solution before this change.

- [ ] **Step 2: Wire the feature into `Program.cs`**

In `working/content/modulith/src/ModulithTemplate.Web/Program.cs`, add the using directive and the `ConfigureOrdersFeature()` call. The file should read:

```csharp
using ModulithTemplate.Features.Orders.Web;
using ModulithTemplate.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureOrdersFeature();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

#pragma warning disable S6966 // Awaitable method should be used
app.Run();
#pragma warning restore S6966 // Awaitable method should be used
```

- [ ] **Step 3: Add the local dev connection string**

`working/content/modulith/src/ModulithTemplate.Web/appsettings.Development.json` currently contains:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Replace it with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "PostgresConnection": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
  }
}
```

This matches the devcontainer's Postgres 18 service (`.devcontainer/docker-compose.yml` at the repo root: user/password/db all `postgres`, forwarded to `localhost:5432`), documented in the root `CLAUDE.md`.

- [ ] **Step 4: Verify the full solution builds warning-clean**

Run: `cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror`
Expected: build succeeds with zero warnings.

- [ ] **Step 5: Verify all tests pass**

Run: `cd working/content/modulith && dotnet test ModulithTemplate.slnx`
Expected: all tests pass, including `FeatureLayerTests` and `FeatureModuleTests`.

- [ ] **Step 6: Verify the scaffold still renames cleanly**

Run from the repo root:

```bash
dotnet new uninstall working/content/modulith 2>/dev/null
dotnet new install working/content/modulith
rm -rf /tmp/dbcontext-plan-verify
dotnet new modulith -n MyApp -o /tmp/dbcontext-plan-verify
grep -rl "ModulithTemplate" /tmp/dbcontext-plan-verify --include="*.cs" --include="*.csproj" --include="*.slnx" --include="*.json"
dotnet new uninstall working/content/modulith
rm -rf /tmp/dbcontext-plan-verify
```

Expected: the `grep` produces no output (empty) — nothing named `ModulithTemplate` leaked into the scaffolded output, including the new `Infrastructure.Common` project, `OrdersContext`, and the `ConnectionStrings` section.

---

### Task 7: Update generated-project `CLAUDE.md`

**Files:**
- Modify: `working/content/modulith/CLAUDE.md`

**Interfaces:**
- Consumes: nothing (documentation only).

- [ ] **Step 1: Rewrite the "EF Core migrations" section**

In `working/content/modulith/CLAUDE.md`, replace the `### EF Core migrations` section **including its trailing "devcontainer provides Postgres" paragraph** (currently lines 30-46, i.e. everything from `### EF Core migrations` up to and including the `The devcontainer provides Postgres 18 ...` line, right up to the blank line before `## Code Style`) with:

```markdown
### EF Core migrations

Each feature owns its own `DbContext`, schema, and migrations in its `*.Infrastructure` project. Startup project is always `ModulithTemplate.Web`.

```bash
# Create a new migration for a feature (Orders shown; repeat per feature)
dotnet ef migrations add "Name" -o Data/Migrations \
  --project src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj

# Apply a feature's pending migrations
dotnet ef database update \
  --project src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj
```

The devcontainer provides Postgres 18 (`localhost:5432`, user/pass/db all `postgres`); the app container shares the db container's network.
```

- [ ] **Step 2: Rewrite the "Architecture" section**

Replace the whole `## Architecture` section **through the end of its `### Dependency injection` subsection** — i.e. everything from `## Architecture` down to (but not including) `### Database access from Blazor components`, which stays unchanged — with:

```markdown
## Architecture

This solution follows a vertical-slice modular monolith: each feature under `src/Features/<Name>/` owns four layer projects forming a dependency chain Web → Application → Domain ← Infrastructure (Domain has no outbound dependencies). Feature modules must not depend on each other (enforced by `ModulithTemplate.ArchitectureTests`).

- **`<Name>.Domain`** — entities, domain services, and abstractions only. Use rich entities: private setters, `internal` constructors, a private parameterless ctor for EF, and invariants enforced in the ctor and mutator methods. Cross-entity rules that need data access live in domain services (`*DomainService`). Data access is abstracted behind `IRepository<T>` (from the shared `ModulithTemplate.FeatureCore` project, extends Ardalis.Specification's `IRepositoryBase<T>`); query logic lives in `Specifications/` as `*Spec` classes.
- **`<Name>.Application`** — orchestration layer. App services (`I*AppService`, `internal` impls) load entities via repositories, invoke domain services, persist, and map to DTOs. They return **FluentResults** `Result`/`Result<T>` — exceptions (except `OperationCanceledException`) are caught and turned into `Result.Fail`; callers branch on `IsFailed`/`Errors`. Entity↔DTO mapping uses **Mapperly** source generators (`Mapper/*Mapper.cs`, `[Mapper]` partial classes).
- **`<Name>.Infrastructure`** — EF Core + Npgsql implementation, owned entirely by the feature: a concrete `DbContext` (schema set via `HasDefaultSchema` in `OnModelCreating`), a context-bound `<Name>Repository<T> : RepositoryBase<T>, IRepository<T>`, and its own `Data/Migrations/`. DB naming is snake_case via `EFCore.NamingConventions`, applied uniformly through the shared `ModulithTemplate.Infrastructure.Common` project's `AddModuleDbContext<TContext>(configuration, schema)` extension — that shared project defines EF conventions only and never a concrete `DbContext`.
- **`<Name>.Web`** — the feature's composition root, the only layer allowed to reference both `Application` and `Infrastructure`. Exposes a `ConfigureXxxFeature(this WebApplicationBuilder)` extension (e.g. `OrdersModule.ConfigureOrdersFeature`) that calls the feature's `ConfigureXxxInfrastructure`/`ConfigureXxxApplication` and registers its endpoints/Razor components.

`ModulithTemplate.Web` is the host / composition root: `Program.cs` calls each feature's `ConfigureXxxFeature()` once — see "Adding a feature" below.

### Where business rules live

Business rules and invariants belong in the entity (or a domain service when they span entities), never in app services, components, or DTO mapping. An app service orchestrates — load, call domain methods, persist, map — it does not *decide* what a valid entity looks like. If the same rule can be violated through more than one call path (e.g. create and update), that is the signal it belongs in the entity, where it is enforced once for all callers.

When adding or changing an invariant, cover it with a `<Name>.DomainTest` test on the entity, not only via the app-service test — the domain is where the guarantee now lives.

### Dependency injection

Each feature layer exposes a `Configuration.cs` with a `ConfigureXxxYyy(this IServiceCollection[, IConfiguration]) : IServiceCollection` extension method (e.g. `ConfigureOrdersInfrastructure`, `ConfigureOrdersApplication`). The feature's `Web` project's `ConfigureXxxFeature` calls its own layers' `Configuration.cs` methods; `Program.cs` calls each feature's `ConfigureXxxFeature` once. When adding a service, register it in the owning layer's `Configuration.cs`, not in `Program.cs` or another feature's composition root.
```

- [ ] **Step 3: Update the "Adding a feature" section**

Replace the `## Adding a feature` section (currently lines 111-126) with:

```markdown
## Adding a feature

Scaffold a new feature's four layer projects and register them in the solution:

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Payments
```

Run this from the solution root (the directory containing the `.slnx`). It creates
`src/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}`
and adds all four to the solution under a `/src/Features/Payments/` folder.

Then wire it in, following the `Orders` feature as a reference:

1. In `Payments.Infrastructure`, add a `PaymentsContext : DbContext`, a `PaymentsRepository<T> : RepositoryBase<T>, IRepository<T>`, and a `Configuration.cs` with `ConfigurePaymentsInfrastructure` that calls `AddModuleDbContext<PaymentsContext>(configuration, schema: "payments")` — see `ModulithTemplate.Infrastructure.Common.ModuleDbContextExtensions`.
2. In `Payments.Web`, add a `PaymentsModule` with `ConfigurePaymentsFeature(this WebApplicationBuilder)` that calls the feature's own `Configuration.cs` methods (needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in the `Payments.Web.csproj`, and a `ProjectReference` to `Payments.Infrastructure`).
3. In the host `ModulithTemplate.Web`, add a `ProjectReference` to `Payments.Web` and call `builder.ConfigurePaymentsFeature();` in `Program.cs`.
```

- [ ] **Step 4: Verify the docs describe only projects that exist**

Run: `grep -n "ModulithTemplate.Infrastructure\b" working/content/modulith/CLAUDE.md`
Expected: no output, or only matches for `ModulithTemplate.Infrastructure.Common` — the old shared `ModulithTemplate.Infrastructure` project name (without `.Common`) must not appear anywhere in the doc after this edit. (The grep pattern `\b` after `Infrastructure` means it also matches `.Common`'s leading dot boundary; if it flags `ModulithTemplate.Infrastructure.Common` lines, that's expected and fine — check that no line refers to the plain project instead.)

---

## Final verification (run after all tasks)

- [ ] Run: `cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror` — expect success, zero warnings.
- [ ] Run: `cd working/content/modulith && dotnet test ModulithTemplate.slnx` — expect all tests pass.
- [ ] Repeat the scaffold-and-grep check from Task 6 Step 6 one more time against the final state of the tree.
- [ ] Read the rewritten `## Architecture`, `### EF Core migrations`, and `## Adding a feature` sections of `working/content/modulith/CLAUDE.md` end-to-end and confirm they describe the tree exactly as it now exists (project names, file paths, method names) — no stale references to `CatalogContext`, `EfRepository`, `IUnitOfWork`, or the old single-`Infrastructure` layout should remain in the sections just rewritten.

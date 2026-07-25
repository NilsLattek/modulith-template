# Feature-owned DbContext & shared infrastructure — design

Date: 2026-07-20
Status: Approved (design)

## Context

The template is mid-migration from a layered monolith (single `Domain`/`Application`/`Infrastructure`/`Web` chain) to a vertical-slice modular monolith. The new architecture tests already encode the target:

- `FeatureModuleTests.ModulesCannotDependOnEachOther` — feature modules must not reference each other.
- `FeatureLayerTests` — within a feature, `Domain` depends on nothing; `Application` and `Infrastructure` depend only on `Domain`; `Web` must not touch `Domain` directly.

The remaining layered artifact is the top-level `ModulithTemplate.Infrastructure`, which today holds a concrete `CatalogContext` (hardcoded `catalog` schema), `EfRepository<T>` bound to that context, and DbContext registration. Hosting one feature's concrete context in a shared project is exactly the cross-feature coupling the new tests forbid.

## Decision

Give **each feature its own `DbContext`, migrations, schema, and self-registration**. Keep a **shared infrastructure project for DbContext conventions only** — no concrete context. Wire features into the host via **explicit `ConfigureFeature` calls in `Program.cs`**.

### Why keep a shared infrastructure project

Snake_case naming, per-schema migrations-history, and exception mapping should be defined once and applied uniformly to every feature context. That shared code references EF Core / Npgsql, so it **must be separate from `FeatureCore`**: `FeatureCore` is a pure abstraction project (`IRepository<T>`, Ardalis only) that feature `Domain` layers reference. Folding EF into it would pull EF Core transitively into every `Domain`, breaking the layering the arch tests enforce.

The shared project is renamed **`ModulithTemplate.Infrastructure.Common`** to signal its convention/shared role.

## Target layout

```
src/
  ModulithTemplate.FeatureCore/           # PURE abstractions — IRepository<T> (Ardalis only). No EF. Referenced by Domain.
  ModulithTemplate.Infrastructure.Common/ # SHARED EF conventions. EF Core/Npgsql/naming/exceptions. NO concrete DbContext.
      ModuleDbContextExtensions.cs        #   AddModuleDbContext<TContext>(config, schema)
  ModulithTemplate.Web.Common/            # shared web helpers (WithNewScopeAsync). Unchanged.
  ModulithTemplate.Web/                   # HOST / composition root. Program.cs calls each feature's ConfigureFeature.
  Features/Orders/
      *.Domain/          # entities; IRepository<T> via FeatureCore
      *.Application/     # app services (Configuration.cs -> ConfigureOrdersApplication)
      *.Infrastructure/  # OrdersContext : DbContext, Data/Migrations/, OrdersRepository<T>, Configuration.cs
      *.Web/             # OrdersModule.ConfigureOrdersFeature(builder) — feature composition root
```

## Components

### Shared: `ModulithTemplate.Infrastructure.Common`

The single place that defines how every feature context is configured. Referenced only by feature `*.Infrastructure` projects.

```csharp
public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration config, string schema)
        where TContext : DbContext =>
        services.AddDbContext<TContext>(o => o
            .UseNpgsql(config.GetConnectionString("PostgresConnection"),
                pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseSnakeCaseNamingConvention()
            .UseExceptionProcessor());   // shared exception mapping slot
}
```

### Per feature: `Features/Orders/*.Infrastructure`

`CatalogContext` moves here and becomes `OrdersContext`; `EfRepository<T>` becomes the thin, context-bound `OrdersRepository<T>`.

```csharp
public class OrdersContext(DbContextOptions<OrdersContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasDefaultSchema("orders");
        b.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

internal sealed class OrdersRepository<T>(OrdersContext ctx)
    : RepositoryBase<T>(ctx), IRepository<T> where T : class;

// Configuration.cs
public static IServiceCollection ConfigureOrdersInfrastructure(this IServiceCollection s, IConfiguration c)
{
    s.AddModuleDbContext<OrdersContext>(c, schema: "orders");
    s.AddScoped(typeof(IRepository<>), typeof(OrdersRepository<>));   // clean open-generic registration
    return s;
}
```

Rationale for the thin per-feature repository: EF's DI cannot auto-close an open generic `IRepository<>` over two type parameters, so a shared `EfRepository<TContext,T>` would force per-entity registration or a factory. A small, context-bound `OrdersRepository<T>` (~3 lines) preserves the clean `AddScoped(typeof(IRepository<>), typeof(OrdersRepository<>))` registration and keeps each feature self-contained.

### Per feature: `Features/Orders/*.Web` — composition root

`ConfigureFeature` lives in the feature's `Web` project, the only layer permitted to reference both `Application` and `Infrastructure`.

```csharp
public static class OrdersModule
{
    public static WebApplicationBuilder ConfigureOrdersFeature(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureOrdersInfrastructure(builder.Configuration);
        builder.Services.ConfigureOrdersApplication();
        // feature endpoints / Razor components registered here too
        return builder;
    }
}
```

### Host: `ModulithTemplate.Web/Program.cs`

```csharp
builder.ConfigureOrdersFeature();
// add each new feature here (one line)
```

## Migrations

Each feature owns its migrations in its own `Infrastructure` project, under its own schema, with an isolated `__EFMigrationsHistory` in that schema — modules migrate independently. Startup project remains the host `ModulithTemplate.Web`.

```bash
dotnet ef migrations add "Name" -o Data/Migrations \
  --project src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj
```

## Docs to update

The generated-project `working/content/modulith/CLAUDE.md` still describes the old four-project layered monolith and single-`Infrastructure` `dotnet ef` commands. Its Architecture and EF Core migrations sections must be rewritten for the feature-owned model.

## Out of scope (YAGNI)

- No `IFeatureModule` marker interface — explicit `ConfigureFeature` calls make it unnecessary.
- No assembly-scanning / auto-discovery of modules.
- No shared `EfRepository` base beyond Ardalis `RepositoryBase<T>`.
- No `IUnitOfWork` reintroduction (not present in current `FeatureCore`).

## Testing

The existing `FeatureModuleTests` and `FeatureLayerTests` continue to guard the boundaries: no cross-feature dependency, and `Infrastructure` depends only on `Domain`. The shared `Infrastructure.Common` project has no `.Features.` segment, so it is outside the arch-test slices by construction and cannot become a cross-feature coupling point.

## Template mechanics

All new identifiers keep the `ModulithTemplate` prefix so `dotnet new modulith` renames them (`ModulithTemplate.Infrastructure.Common`, `OrdersContext` inside `ModulithTemplate.Features.Orders.Infrastructure`, etc.). After the change, scaffold into a temp dir and confirm nothing named `ModulithTemplate` leaks.

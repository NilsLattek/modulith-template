# Modulith

`dotnet new` templates for a .NET modular monolith: a Blazor Server host plus one project
quartet (`Domain`/`Application`/`Infrastructure`/`Web`) per feature, each feature owning its own
`DbContext` and its own Postgres schema.

## Templates in this package

- `dotnet new modulith` — scaffolds a new modular-monolith solution.
- `dotnet new modulith-feature --appName <App> -n <Name>` — run from a scaffolded solution's
  root to add one feature's four layer projects (`Domain`/`Application`/`Infrastructure`/`Web`)
  plus their four matching test projects, eight projects in all, registered in the solution
  automatically.

A scaffolded feature is added to the `.slnx` but not yet wired into the host. Reference its
`Web` project from `src/<App>.Web/` and call `builder.Configure<Name>Feature();` in `Program.cs`
— the generated solution's own README spells out both commands.

## What a scaffolded solution contains

- A sample `Orders` feature showing the layering, with command/query handlers, a repository, and
  its own `OrdersContext`.
- EF Core on Npgsql with snake_case naming and a per-feature migrations history table.
- [Mediator](https://github.com/martinothamar/Mediator) with logging, exception, and validation
  pipeline behaviours; FluentResults for handler outcomes and FluentValidation for input.
- xUnit v3 test projects per layer, bUnit for components, and ArchUnitNET architecture tests.
- A devcontainer with .NET, the `dotnet-ef` CLI, and Postgres.

## DB Migrations

Each feature has its own `DbContext` and schema, so every `dotnet ef` command needs a `--context`.
The generated solution ships two scripts in its root that handle that:

```bash
bash add-migration.sh Orders InitialOrders   # <FeatureName> <MigrationName>
bash update-database.sh                      # applies every feature's pending migrations
```

`update-database.sh` discovers the contexts from the host's DI container, so a feature is picked
up as soon as `Program.cs` registers it.

The equivalent raw commands, if you need to deviate (replace `MyApp`/`Orders` with your own):

```bash
dotnet ef migrations add InitialOrders -o Data/Migrations --context OrdersContext \
  --project src/Features/Orders/MyApp.Features.Orders.Infrastructure/ \
  -s src/MyApp.Web/

dotnet ef database update --context OrdersContext \
  --project src/MyApp.Web/ -s src/MyApp.Web/
```

The `dotnet-ef` CLI comes preinstalled in the generated devcontainer; outside it, install with
`dotnet tool install --global dotnet-ef`. `Microsoft.EntityFrameworkCore.Design` is already
referenced by the web host, so there is no package to add.

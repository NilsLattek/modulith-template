# ModulithTemplate

A modular monolith. Each feature under `src/Features/` owns its own layers (`Contracts`, `Domain`, `Application`, `Infrastructure`, `Web`), its own `DbContext`, and its own Postgres schema.

## Setup

Open the repo in the devcontainer — it provides .NET, the `dotnet-ef` CLI, and Postgres 18 on `localhost:5432` (user / password / database all `postgres`).

```bash
dotnet restore
dotnet build --no-restore
```

## Run project

```bash
cd src/ModulithTemplate.Web/
dotnet run --no-restore
```

## Test

```bash
dotnet test --no-restore
```

## Migrations

Each feature has its own `DbContext` and schema, so every `dotnet ef` command needs a `--context`. The two scripts in this directory take care of that for you.

Create a new migration for a feature:

```bash
bash add-migration.sh Orders InitialOrders     # <FeatureName> <MigrationName>
```

Apply all pending migrations of every feature to the local database:

```bash
bash update-database.sh
```

`update-database.sh` discovers the contexts from the host's DI container, so a new feature is included as soon as it's registered in `Program.cs` — nothing to add to the script.

The equivalent raw commands, if you need to deviate:

```bash
dotnet ef migrations add "InitialOrders" -o Data/Migrations --context OrdersContext \
  --project src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj

dotnet ef database update --context OrdersContext \
  --project src/ModulithTemplate.Web/ModulithTemplate.Web.csproj \
  -s src/ModulithTemplate.Web/ModulithTemplate.Web.csproj
```

## Add a feature

From the solution root, scaffold the feature's layer projects plus their test projects:

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Payments
```

Then register it with the host:

```bash
dotnet add src/ModulithTemplate.Web/ModulithTemplate.Web.csproj reference \
  src/Features/Payments/ModulithTemplate.Features.Payments.Web/ModulithTemplate.Features.Payments.Web.csproj
```

and call `builder.ConfigurePaymentsFeature();` in `src/ModulithTemplate.Web/Program.cs`.

## Observability

`src/ModulithTemplate.ServiceDefaults/` is a near-verbatim copy of .NET Aspire's ServiceDefaults project. `builder.AddServiceDefaults()` in `Program.cs` turns on OpenTelemetry tracing, metrics and logging, instrumenting ASP.NET Core, `HttpClient`, the .NET runtime and Npgsql — so a database query shows up as a child span of the request that issued it.

**Nothing is exported unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set.** With the variable unset, which is the default, telemetry is collected and then discarded. To actually see it, point the app at an OTLP collector:

```bash
cd src/ModulithTemplate.Web/
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --no-restore
```

Any OTLP collector works. The standalone [.NET Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone) is the quickest option for local use.

### Health checks

`app.MapDefaultEndpoints()` maps `/health` (every check must pass) and `/alive` (only checks tagged `live`).

Both are mapped **in the Development environment only**, because they are unauthenticated — so a production deployment has no health endpoint until you add one. To expose them, put authentication in front and relax the `IsDevelopment()` guard in `src/ModulithTemplate.ServiceDefaults/Extensions.cs`.

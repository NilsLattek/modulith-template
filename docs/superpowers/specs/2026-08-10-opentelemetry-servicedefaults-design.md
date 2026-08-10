# Adding OpenTelemetry to the generated modulith project

**Date:** 2026-08-10
**Status:** Approved, not yet implemented

## Goal

A project scaffolded by `dotnet new modulith` should come with OpenTelemetry tracing, metrics and
logging, plus health check endpoints, already wired into the host. The mechanism is a copy of
.NET Aspire's `ServiceDefaults` project, kept as close to upstream as this repository's analyzer
settings allow.

The generated app is a single-process modular monolith, not an Aspire-orchestrated distributed
system. It has no AppHost and no sibling services.

## Decisions

| Question | Decision |
| --- | --- |
| How much of ServiceDefaults? | **All of it**, verbatim — including `AddServiceDiscovery()` and the standard HTTP resilience handler |
| Where does telemetry go? | **Nowhere by default.** `OTEL_EXPORTER_OTLP_ENDPOINT` stays unset; no dashboard or collector container is added |
| Database instrumentation? | **Yes** — one added line so Npgsql spans nest under request spans |
| Where does the code live? | **Its own project**, `src/ModulithTemplate.ServiceDefaults/` |
| Bump the template package version? | **Yes** — `0.3` → `0.4` |

### Why verbatim, including the parts this solution does not use

Service discovery and `AddStandardResilienceHandler()` exist to wire one service to another. In a
single-process monolith they register configuration that nothing consumes, and they cost two extra
package references. They are kept anyway so the file diffs cleanly against future Aspire versions,
and so they are already in place if the generated app later makes outbound HTTP calls.

### Why no collector

Shipping an Aspire dashboard container in the devcontainer would make the feature visible on day
one, but adds a container to every generated project whether or not its author wants one. The
export path is instead documented, and the user points `OTEL_EXPORTER_OTLP_ENDPOINT` at whatever
they run.

Consequence to be explicit about in the docs: **with no endpoint set, telemetry is collected and
silently discarded.** `AddOpenTelemetryExporters` gates `UseOtlpExporter()` on that variable being
non-empty.

## Source of the copy

`dotnet new aspire-servicedefaults`, from `Aspire.ProjectTemplates` **13.4.6**, which produces
`Extensions.cs` and a `.csproj`. Record this version in the file header so a future update knows
what it is re-basing from.

## The new project

`src/ModulithTemplate.ServiceDefaults/ModulithTemplate.ServiceDefaults.csproj`, a
`Microsoft.NET.Sdk` class library. The `ModulithTemplate.` prefix is required — `sourceName` is
`ModulithTemplate`, so anything without it leaks the template's name into generated projects.

`<FrameworkReference Include="Microsoft.AspNetCore.App" />` is retained: it is what lets a plain
class library see `WebApplication`, `MapHealthChecks` and `IHostApplicationBuilder`.

### Adaptations to the scaffolded `.csproj`

Three mechanical changes, none affecting behaviour:

1. Remove `TargetFramework`, `ImplicitUsings` and `Nullable` — `Directory.Build.props` sets all
   three solution-wide.
2. Strip `Version="…"` from every `PackageReference` and add a matching `PackageVersion` to
   `Directory.Packages.props`. Central package management is mandatory in this solution.
3. Drop `<IsAspireSharedProject>true</IsAspireSharedProject>` — it is only meaningful to Aspire
   AppHost tooling, which this solution does not have.

### Packages to add to `Directory.Packages.props`

Versions as scaffolded by Aspire 13.4.6; confirm each resolves before committing.

| Package | Version |
| --- | --- |
| `Microsoft.Extensions.Http.Resilience` | 10.6.0 |
| `Microsoft.Extensions.ServiceDiscovery` | 10.6.0 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.3 |
| `OpenTelemetry.Extensions.Hosting` | 1.15.3 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.15.2 |
| `OpenTelemetry.Instrumentation.Http` | 1.15.1 |
| `OpenTelemetry.Instrumentation.Runtime` | 1.15.1 |

`CentralPackageTransitivePinningEnabled` is `true` in this solution, so each of these pins now
governs transitive resolution as well. A pin set below some other package's transitive requirement
surfaces as `NU1109`. If that happens, raise the pin rather than removing it.

## The one deliberate code change: Npgsql tracing

Upstream instruments ASP.NET Core, `HttpClient` and the runtime, but not the database. Every
feature in this solution owns an Npgsql `DbContext`, so without this, database work is invisible in
a trace.

Add one line to the `WithTracing` block of `ConfigureOpenTelemetry`:

```csharp
.AddSource("Npgsql")
```

A bare source name rather than the `Npgsql.OpenTelemetry` package's `AddNpgsql()`, because that
package's extension method does nothing but subscribe to this exact source. Same spans, no eighth
package, and no Npgsql reference from `ServiceDefaults`.

**To verify during implementation:** confirm `"Npgsql"` is still the `ActivitySource` name in
Npgsql 10. If it has changed, use the `Npgsql.OpenTelemetry` package instead and note the
deviation.

## Analyzer conflict with "verbatim"

`Directory.Build.props` injects Meziantou, SonarAnalyzer and Roslynator into every project, sets
`EnforceCodeStyleInBuild`, and CI builds with `-warnaserror`. Aspire's file is written for a
template carrying none of that, so it is unlikely to compile clean as-is.

One failure is near-certain: `using Microsoft.Extensions.ServiceDiscovery;` is referenced only from
a commented-out block, which `IDE0005` will flag. Others may surface.

**Resolution policy, in order of preference:**

1. Narrow `#pragma warning disable <id>` with a comment saying why, matching `restore` after.
2. If a pragma will not do, the smallest possible edit to the code.
3. Never a global or project-wide suppression.

Every deviation from upstream must be listed in a header comment at the top of `Extensions.cs`, so
a future re-base can see at a glance what was changed and why.

XML documentation comments are **not** added to the public surface. The repository's style guide
asks for them, but they are not analyzer-enforced here (`GenerateDocumentationFile` is unset), and
rewriting Aspire's comments would defeat the point of keeping the file diffable.

## Host wiring

In `src/ModulithTemplate.Web/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // first: OTel logging in place before anything else registers
builder.ConfigureOrdersFeature();
// ...
var app = builder.Build();
// ...
app.MapDefaultEndpoints();
```

No `using` directive is needed. Aspire declares the extensions in the `Microsoft.Extensions.Hosting`
namespace, which the Web SDK's implicit usings already include.

Also required:

- A `ProjectReference` from `ModulithTemplate.Web.csproj` to the new project.
- An entry in the `/src/` folder of `ModulithTemplate.slnx`.

`working/content/modulith/.template.config/template.json` needs no change — the package project
packs `content/**` wholesale, with no per-project file list.

### Health endpoints are Development-only

`MapDefaultEndpoints` maps `/health` and `/alive` only when
`app.Environment.IsDevelopment()`, because they are unauthenticated. This is upstream's deliberate
choice and is being kept as-is: **a generated app has no health endpoint in production** until its
author opts in. Documented, not changed.

## What does not change

`working/content/feature/` — the `modulith-feature` sub-template — is untouched. Features inherit
ASP.NET Core, `HttpClient` and Npgsql instrumentation through the host. A feature wanting its own
custom spans registers its own `ActivitySource`; note this in the generated `CLAUDE.md`.

The repository-root `CLAUDE.md` is untouched; its layout section is generic.

## Documentation, inside the generated project

**`README.md`** — a new "Observability" section covering: telemetry is collected but exported only
when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; how to point it at a collector or a standalone Aspire
dashboard; and that `/health` and `/alive` exist only in Development.

**`CLAUDE.md`** — a few lines under Architecture: that `ServiceDefaults` exists, that it is
near-verbatim Aspire and should stay that way, and that custom spans need their own
`ActivitySource`.

## Tests

One `ServiceDefaultsTests` class in the existing `ModulithTemplate.WebTests` project. No new
packages and no `WebApplicationFactory`: build a host builder, call `AddServiceDefaults()`, then
assert

- the `self` health check, tagged `live`, is registered;
- the tracer and meter providers resolve from the container;
- no OTLP exporter is configured when `OTEL_EXPORTER_OTLP_ENDPOINT` is absent.

This is a smoke test. Its purpose is to catch a future package bump that breaks registration, not
to test Aspire's code.

## Version bump

`<PackageVersion>` in `working/ModularMonolith.Template.csproj`: `0.3` → `0.4`.

## Verification

All four must pass before this is considered done.

1. `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror` — clean.
2. `cd working/content/modulith && dotnet test` — green. (Must run from the content directory;
   `global.json` opts into Microsoft.Testing.Platform and is resolved from the current directory.)
3. Scaffold end-to-end: `dotnet new install working/content/modulith`, then
   `dotnet new modulith -n MyApp -o /tmp/MyApp`; grep the output for any surviving
   `ModulithTemplate` string; build the generated solution with `-warnaserror`; uninstall.
4. Confirm the Npgsql `ActivitySource` name **statically** — read the `new ActivitySource(...)`
   call in the Npgsql 10 assembly (or the `AddNpgsql()` implementation in
   `Npgsql.OpenTelemetry`) and check it against the string in `AddSource(...)`.

**Not verified:** no end-to-end run against a live OTLP endpoint. Steps 1–3 all pass regardless of
whether the source name is right, so step 4's static check is the only thing standing behind the
Npgsql line. If that string is wrong in a way the static check misses, DB spans are silently
missing and nothing in the build or test suite will say so.

## Git

No git actions. The repository maintainer handles all commits, branches and releases.

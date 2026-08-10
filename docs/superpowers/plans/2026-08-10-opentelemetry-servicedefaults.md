# OpenTelemetry / Aspire ServiceDefaults Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A project scaffolded by `dotnet new modulith` ships with OpenTelemetry tracing, metrics and logging plus health check endpoints, via a near-verbatim copy of .NET Aspire's ServiceDefaults project.

**Architecture:** A new class library `src/ModulithTemplate.ServiceDefaults/` holds one file, `Extensions.cs`, copied from `dotnet new aspire-servicedefaults`. The host project references it and calls `AddServiceDefaults()` and `MapDefaultEndpoints()`. Everything else in the template is untouched — features inherit instrumentation through the host.

**Tech Stack:** .NET 10, OpenTelemetry 1.15.x, `Microsoft.Extensions.ServiceDiscovery` / `Http.Resilience` 10.6.0, xUnit v3 on Microsoft.Testing.Platform.

**Design spec:** `docs/superpowers/specs/2026-08-10-opentelemetry-servicedefaults-design.md`

---

## Global Constraints

These apply to every task below.

- **No git actions.** Do not `git add`, `git commit`, `git branch`, or `git push`. The repository maintainer handles all git. Tasks end at a verified working tree, not a commit.
- **All work happens under `working/content/modulith/`** — that is the template source that becomes the user's solution. The repository root's own `.github`, `.devcontainer` and `.editorconfig` are for developing *this* repo and are not touched.
- **`sourceName` is `ModulithTemplate`.** Every new project, namespace and directory must carry the `ModulithTemplate` prefix, or the template's name leaks into generated projects.
- **Central package management is mandatory.** Every `PackageReference` is version-less; versions live in `working/content/modulith/Directory.Packages.props` as `PackageVersion`. `CentralPackageTransitivePinningEnabled` is `true`, so each pin governs transitive resolution too — an `NU1109` means the pin is *below* some package's transitive requirement and must be **raised**, never removed.
- **The build must be warning-clean under `-warnaserror`.** Meziantou, SonarAnalyzer and Roslynator run on build with `EnforceCodeStyleInBuild`.
- **Suppress narrowly.** `#pragma warning disable <id>` with a comment explaining why, plus a matching `restore`. Never a project-wide or global suppression.
- **Keep `Extensions.cs` diffable against upstream Aspire.** Prefer a pragma over editing Aspire's code. Every deviation is listed in the file's header comment.
- **Do not add XML doc comments to `Extensions.cs`.** The generated project's `CLAUDE.md` asks for them on public members, but they are not analyzer-enforced (`GenerateDocumentationFile` is unset) and rewriting Aspire's comments would defeat the point of keeping the file diffable. This is a deliberate, spec-level exception — the new **test** file does follow the repo's XML-comment convention.
- **Build command:** `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror`
- **Test command:** must run from the content directory — `cd working/content/modulith && dotnet test`. `global.json` opts into Microsoft.Testing.Platform and is resolved from the current directory; passing the `.slnx` as a path from the repo root silently runs zero tests.

---

## File Structure

**Created:**

| File | Responsibility |
| --- | --- |
| `working/content/modulith/src/ModulithTemplate.ServiceDefaults/ModulithTemplate.ServiceDefaults.csproj` | Package references and the ASP.NET Core framework reference |
| `working/content/modulith/src/ModulithTemplate.ServiceDefaults/Extensions.cs` | The whole public surface: `AddServiceDefaults`, `ConfigureOpenTelemetry`, `AddDefaultHealthChecks`, `MapDefaultEndpoints` |
| `working/content/modulith/test/ModulithTemplate.WebTests/ServiceDefaultsTests.cs` | Registration smoke tests |

**Modified:**

| File | Change |
| --- | --- |
| `working/content/modulith/Directory.Packages.props` | 7 new `PackageVersion` entries |
| `working/content/modulith/ModulithTemplate.slnx` | 1 new `<Project>` entry under `/src/` |
| `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj` | 1 new `ProjectReference` |
| `working/content/modulith/src/ModulithTemplate.Web/Program.cs` | 2 call sites |
| `working/content/modulith/README.md` | New "Observability" section |
| `working/content/modulith/CLAUDE.md` | New "### Observability" subsection |
| `working/ModularMonolith.Template.csproj` | `<PackageVersion>` `0.3` → `0.4` |

**Untouched:** `working/content/feature/` (the `modulith-feature` sub-template), `working/content/modulith/.template.config/template.json` (the package project packs `content/**` wholesale, with no per-project file list), and the repository-root `CLAUDE.md`.

---

## Task 1: The ServiceDefaults project

Creates the project and gets it building clean in isolation. Nothing references it yet, so the rest of the solution cannot break.

**Files:**
- Create: `working/content/modulith/src/ModulithTemplate.ServiceDefaults/ModulithTemplate.ServiceDefaults.csproj`
- Create: `working/content/modulith/src/ModulithTemplate.ServiceDefaults/Extensions.cs`
- Modify: `working/content/modulith/Directory.Packages.props`
- Modify: `working/content/modulith/ModulithTemplate.slnx`

**Interfaces:**
- Consumes: nothing.
- Produces, all in namespace `Microsoft.Extensions.Hosting`, all on `public static class Extensions`:
  - `public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder`
  - `public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder`
  - `public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder`
  - `public static WebApplication MapDefaultEndpoints(this WebApplication app)`

---

- [ ] **Step 1: Confirm the Npgsql `ActivitySource` name**

`Extensions.cs` will contain `.AddSource("Npgsql")`. Confirm that string is still correct for the Npgsql version this solution pins (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, which resolves `Npgsql` 10.0.2) before writing it.

Run:

```bash
curl -s https://raw.githubusercontent.com/npgsql/npgsql/main/src/Npgsql.OpenTelemetry/TracerProviderBuilderExtensions.cs | grep -n "AddSource"
```

Expected: a line passing the literal `"Npgsql"`.

If it is anything else, use that string instead and note the change in the file header comment. Do **not** silently keep `"Npgsql"`.

- [ ] **Step 2: Add the seven package versions**

In `working/content/modulith/Directory.Packages.props`, inside the existing `<ItemGroup>`, add these two blocks. Place the `Microsoft.Extensions.*` pair immediately after the existing `Microsoft.Extensions.DependencyInjection.Abstractions` line, and the `OpenTelemetry.*` block immediately after the existing `NSubstitute` line, keeping the file's rough alphabetical grouping.

```xml
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="10.6.0" />
    <PackageVersion Include="Microsoft.Extensions.ServiceDiscovery" Version="10.6.0" />
```

```xml
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.3" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.15.3" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.15.2" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.15.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.15.1" />
```

- [ ] **Step 3: Create the project file**

Create `working/content/modulith/src/ModulithTemplate.ServiceDefaults/ModulithTemplate.ServiceDefaults.csproj` with exactly this content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />

    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
  </ItemGroup>

</Project>
```

Note what is deliberately absent versus what `dotnet new aspire-servicedefaults` scaffolds: `TargetFramework`, `ImplicitUsings` and `Nullable` come from `Directory.Build.props`; `Version=` attributes come from `Directory.Packages.props`; and `<IsAspireSharedProject>true</IsAspireSharedProject>` is dropped because it only means something to Aspire AppHost tooling, which this solution does not have.

`<FrameworkReference Include="Microsoft.AspNetCore.App" />` must stay — it is what lets a plain class library see `WebApplication`, `MapHealthChecks` and `IHostApplicationBuilder`.

- [ ] **Step 4: Create `Extensions.cs`**

Create `working/content/modulith/src/ModulithTemplate.ServiceDefaults/Extensions.cs` with exactly this content. This is Aspire's scaffolded file after `dotnet format`, plus the Npgsql line, plus two pragmas — this exact text has been verified to build clean under `-warnaserror` with this solution's analyzer set.

```csharp
// Copied from `dotnet new aspire-servicedefaults` (Aspire.ProjectTemplates 13.4.6).
// Kept as close to upstream as this solution's analyzers allow, so it can be re-based on a future
// Aspire version with a small diff. Every deviation is listed here and marked in place below:
//   * `.AddSource("Npgsql")` added, so database calls appear as child spans of their request.
//   * `#pragma warning disable MA0074` and `S3241` around code upstream writes differently.
//   * Project properties come from Directory.Build.props and package versions from
//     Directory.Packages.props, so the .csproj carries neither.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    // Npgsql's own ActivitySource, so database calls appear as child spans of the
                    // request that issued them. Equivalent to Npgsql.OpenTelemetry's AddNpgsql(),
                    // which does nothing but subscribe to this same source name.
                    .AddSource("Npgsql")
#pragma warning disable MA0074 // PathString.StartsWithSegments is ordinal-ignore-case by default; upstream Aspire relies on that
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
#pragma warning restore MA0074
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

#pragma warning disable S3241 // Return type kept as TBuilder to match upstream Aspire's fluent shape
    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }
#pragma warning restore S3241

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
```

- [ ] **Step 5: Register the project in the solution**

In `working/content/modulith/ModulithTemplate.slnx`, add one line inside the `<Folder Name="/src/">` element, keeping the existing alphabetical order — it goes after `ModulithTemplate.Infrastructure.Common` and before `ModulithTemplate.Web.Common`:

```xml
    <Project Path="src/ModulithTemplate.ServiceDefaults/ModulithTemplate.ServiceDefaults.csproj" />
```

- [ ] **Step 6: Build and confirm it is clean**

Run:

```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
```

Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

If restore fails with **NU1109**, a central pin sits below a transitive requirement. Raise the offending `PackageVersion` in `Directory.Packages.props` to the version the error names. Do not delete the pin — `CentralPackageTransitivePinningEnabled` is on deliberately.

If the build reports analyzer errors in `Extensions.cs`, the file text drifted from the verified version above. Re-check it character-for-character before adding new pragmas. The three rules that were verified as needing suppression are exactly `MA0074` (×2) and `S3241` (×1); `IDE0055` formatting issues in Aspire's raw scaffold are already fixed in the text above.

- [ ] **Step 7: Checkpoint**

Do not commit — the maintainer handles git. Report: the project builds clean, is registered in the `.slnx`, and nothing references it yet.

---

## Task 2: Wire ServiceDefaults into the host

**Files:**
- Modify: `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/Program.cs`

**Interfaces:**
- Consumes: `AddServiceDefaults<TBuilder>()` and `MapDefaultEndpoints(WebApplication)` from Task 1.
- Produces: a host whose service provider has `TracerProvider`, `MeterProvider` and a `self` health check registered — Task 3 asserts on exactly these.

---

- [ ] **Step 1: Add the project reference**

In `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj`, in the `<ItemGroup>` that already holds the two `ProjectReference` elements, add as the **first** entry (keeping the existing two below it):

```xml
    <ProjectReference Include="../ModulithTemplate.ServiceDefaults/ModulithTemplate.ServiceDefaults.csproj" />
```

- [ ] **Step 2: Call `AddServiceDefaults` in `Program.cs`**

In `working/content/modulith/src/ModulithTemplate.Web/Program.cs`, find:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.ConfigureOrdersFeature();
```

Replace with:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Aspire's ServiceDefaults: OpenTelemetry tracing, metrics and logging, health checks, service
// discovery and HTTP resilience. First, so telemetry is in place before anything else registers.
builder.AddServiceDefaults();

builder.ConfigureOrdersFeature();
```

No `using` directive is needed. Aspire declares these extensions in the `Microsoft.Extensions.Hosting` namespace, which the Web SDK's implicit usings already include. **Do not add one** — an unnecessary using is a style violation here.

- [ ] **Step 3: Call `MapDefaultEndpoints` in `Program.cs`**

In the same file, find:

```csharp
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

Replace with:

```csharp
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Maps /health and /alive — in the Development environment only, because they are
// unauthenticated. See the security note in ServiceDefaults/Extensions.cs.
app.MapDefaultEndpoints();
```

- [ ] **Step 4: Build**

Run:

```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
```

Expected: `Build succeeded.` with `0 Warning(s)`.

- [ ] **Step 5: Run the existing tests to confirm nothing regressed**

Run:

```bash
cd working/content/modulith && dotnet test
```

Expected: all existing tests pass. In particular `ModulithTemplate.ArchitectureTests` must still pass — it discovers assemblies whose file name contains `.Features.`, and `ModulithTemplate.ServiceDefaults` does not match, so it should be invisible to those rules. If an architecture test now fails, stop and report rather than loosening the rule.

- [ ] **Step 6: Checkpoint**

Do not commit. Report: build clean, all tests green.

---

## Task 3: Registration smoke tests

These are characterization tests over code that already exists, so there is no red-then-green cycle. Step 3 substitutes for it: the tests are proved to have teeth by temporarily removing the wiring and confirming they fail.

**Files:**
- Create: `working/content/modulith/test/ModulithTemplate.WebTests/ServiceDefaultsTests.cs`

**Interfaces:**
- Consumes: `AddServiceDefaults<TBuilder>()` and `MapDefaultEndpoints(WebApplication)` from Task 1.
- Produces: nothing consumed by later tasks.

The test project needs no new package references. `ModulithTemplate.WebTests` references `ModulithTemplate.Web`, which now references `ModulithTemplate.ServiceDefaults`, so the OpenTelemetry and health-check types flow through transitively.

---

- [ ] **Step 1: Write the tests**

Create `working/content/modulith/test/ModulithTemplate.WebTests/ServiceDefaultsTests.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ModulithTemplate.WebTests;

/// <summary>
/// Registration smoke tests for the Aspire ServiceDefaults copy in
/// <c>ModulithTemplate.ServiceDefaults</c>. These exist to catch a package upgrade that
/// silently stops registering telemetry or the health checks, not to test Aspire's own code.
/// </summary>
public class ServiceDefaultsTests
{
    private static WebApplicationBuilder CreateBuilder(string environmentName) =>
        WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });

    private static List<string?> MappedRoutes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

    [Fact]
    public async Task add_service_defaults_registers_the_self_health_check_tagged_live()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Development);
        builder.AddServiceDefaults();

        // Act
        await using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

        // Assert
        var self = Assert.Single(options.Value.Registrations, registration => registration.Name == "self");
        Assert.Contains("live", self.Tags);
    }

    [Fact]
    public async Task add_service_defaults_registers_the_tracer_and_meter_providers()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Development);
        builder.AddServiceDefaults();

        // Act
        await using var app = builder.Build();

        // Assert
        Assert.NotNull(app.Services.GetService<TracerProvider>());
        Assert.NotNull(app.Services.GetService<MeterProvider>());
    }

    [Fact]
    public async Task map_default_endpoints_maps_health_and_alive_in_development()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Development);
        builder.AddServiceDefaults();
        await using var app = builder.Build();

        // Act
        app.MapDefaultEndpoints();

        // Assert
        var routes = MappedRoutes(app);
        Assert.Contains("/health", routes);
        Assert.Contains("/alive", routes);
    }

    [Fact]
    public async Task map_default_endpoints_maps_nothing_outside_development()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Production);
        builder.AddServiceDefaults();
        await using var app = builder.Build();

        // Act
        app.MapDefaultEndpoints();

        // Assert
        Assert.DoesNotContain("/health", MappedRoutes(app));
        Assert.DoesNotContain("/alive", MappedRoutes(app));
    }
}
```

- [ ] **Step 2: Run the tests**

Run:

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.WebTests --filter-class "*ServiceDefaultsTests*"
```

Expected: 4 tests, all passing.

If the build of the test project fails on an analyzer rule, fix it narrowly in the test file — do not weaken `test/Directory.Build.props`. If `WebApplication.CreateBuilder` throws because it cannot find a content root, pass `ContentRootPath = AppContext.BaseDirectory` in the `WebApplicationOptions`.

- [ ] **Step 3: Prove the tests have teeth**

A passing test that would pass anyway is worthless. These tests build their own host, so `Program.cs` is not what they exercise — mutate `Extensions.cs` instead.

In `working/content/modulith/src/ModulithTemplate.ServiceDefaults/Extensions.cs`, temporarily comment out the single statement in the body of `AddDefaultHealthChecks` (the `builder.Services.AddHealthChecks()...AddCheck(...)` call), leaving `return builder;` in place so it still compiles.

Run the same command as Step 2.

Expected: `add_service_defaults_registers_the_self_health_check_tagged_live` **FAILS**, and the two `map_default_endpoints_*` tests still pass.

Then restore the commented-out statement and re-run to confirm all 4 pass again. If the health-check test passed while the registration was commented out, the test is not asserting what it claims — fix it before continuing.

- [ ] **Step 4: Full test run**

Run:

```bash
cd working/content/modulith && dotnet test
```

Expected: every test in the solution passes.

- [ ] **Step 5: Checkpoint**

Do not commit. Report: 4 new tests, verified to fail when the registration is removed.

---

## Task 4: Documentation in the generated project

Both files ship to the user's scaffolded solution, so they are written for that reader, not for someone maintaining this template repo.

**Files:**
- Modify: `working/content/modulith/README.md`
- Modify: `working/content/modulith/CLAUDE.md`

**Interfaces:**
- Consumes: the behaviour established in Tasks 1–2.
- Produces: nothing consumed by later tasks.

---

- [ ] **Step 1: Check whether Docker is usable in this devcontainer**

The README section below contains a `docker run` command for a standalone Aspire dashboard. Verify it is not nonsense before shipping it into every generated project.

Run:

```bash
docker run --rm -d --name aspire-dash-check \
  -p 18888:18888 -p 4317:18889 \
  -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
  mcr.microsoft.com/dotnet/aspire-dashboard:latest \
  && sleep 10 && curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:18888 ; \
  docker rm -f aspire-dash-check 2>/dev/null
```

Expected: `200`.

**If Docker is unavailable in this devcontainer, or the command does not return 200:** do not ship an unverified command. Replace the fenced `docker run` block in Step 2 with this sentence instead, and note the substitution in your checkpoint report:

> Any OTLP collector works. The standalone [.NET Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone) is the quickest option for local use.

- [ ] **Step 2: Add the README section**

In `working/content/modulith/README.md`, insert this section between the existing `## Test` and `## Migrations` sections:

````markdown
## Observability

`src/ModulithTemplate.ServiceDefaults/` is a near-verbatim copy of .NET Aspire's ServiceDefaults project. `builder.AddServiceDefaults()` in `Program.cs` turns on OpenTelemetry tracing, metrics and logging, instrumenting ASP.NET Core, `HttpClient`, the .NET runtime and Npgsql — so a database query shows up as a child span of the request that issued it.

**Nothing is exported unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set.** With the variable unset, which is the default, telemetry is collected and then discarded. To actually see it, point the app at an OTLP collector:

```bash
# One terminal: a standalone Aspire dashboard, UI on 18888, OTLP on 4317
docker run --rm -p 18888:18888 -p 4317:18889 \
  -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
  mcr.microsoft.com/dotnet/aspire-dashboard:latest

# Another terminal: run the app against it
cd src/ModulithTemplate.Web
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run
```

### Health checks

`app.MapDefaultEndpoints()` maps `/health` (every check must pass) and `/alive` (only checks tagged `live`).

Both are mapped **in the Development environment only**, because they are unauthenticated — so a production deployment has no health endpoint until you add one. To expose them, put authentication in front and relax the `IsDevelopment()` guard in `src/ModulithTemplate.ServiceDefaults/Extensions.cs`.
````

- [ ] **Step 3: Add the CLAUDE.md subsection**

In `working/content/modulith/CLAUDE.md`, insert this subsection immediately after the `### Infrastructure` subsection and before `### Dependency injection`:

```markdown
### Observability

`src/ModulithTemplate.ServiceDefaults/` is a near-verbatim copy of .NET Aspire's ServiceDefaults;
`Program.cs` calls `AddServiceDefaults()` and `MapDefaultEndpoints()`. **Keep it close to upstream** —
every deviation is listed in the header comment of `Extensions.cs`, so re-basing onto a newer Aspire
version stays a small diff. Instrumentation covers ASP.NET Core, `HttpClient`, the runtime and
Npgsql; nothing is exported unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set, and `/health` and `/alive`
are mapped in Development only. A feature wanting its own spans declares a
`static readonly ActivitySource` and registers its name with `.AddSource(...)` in
`ConfigureOpenTelemetry`.
```

- [ ] **Step 4: Verify the markdown renders and links are right**

Read both files back and confirm: heading levels are consistent with their neighbours, the fenced blocks are balanced, and the paths named (`src/ModulithTemplate.ServiceDefaults/Extensions.cs`) exist.

- [ ] **Step 5: Checkpoint**

Do not commit. Report whether the `docker run` block was kept or replaced per Step 1.

---

## Task 5: End-to-end template verification and version bump

The real gate. Everything so far verified the template *source* builds; this verifies that a project *scaffolded from it* builds, with no trace of the template's own name.

**Files:**
- Modify: `working/ModularMonolith.Template.csproj`

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: nothing.

---

- [ ] **Step 1: Bump the package version**

In `working/ModularMonolith.Template.csproj`, change:

```xml
    <PackageVersion>0.3</PackageVersion>
```

to:

```xml
    <PackageVersion>0.4</PackageVersion>
```

- [ ] **Step 2: Install the template from source and scaffold a project**

Run:

```bash
dotnet new install /workspaces/modulith-template/working/content/modulith
dotnet new modulith -n MyApp -o /tmp/otel-check/MyApp
```

Expected: the template installs as `modulith` and scaffolds without error.

- [ ] **Step 3: Confirm the template's own name did not leak**

Run:

```bash
grep -rl "ModulithTemplate" /tmp/otel-check/MyApp --exclude-dir=bin --exclude-dir=obj ; echo "exit=$?"
find /tmp/otel-check/MyApp -name "*ModulithTemplate*" -not -path "*/bin/*" -not -path "*/obj/*"
```

Expected: no output from either command (grep exits 1 with no matches). Any hit — especially anything under `ServiceDefaults` — means a `ModulithTemplate` token was introduced somewhere the engine did not rename. Fix it before continuing.

Confirm positively that the rename *did* happen for the new project:

```bash
ls /tmp/otel-check/MyApp/src/MyApp.ServiceDefaults/
grep -n "AddServiceDefaults\|MapDefaultEndpoints" /tmp/otel-check/MyApp/src/MyApp.Web/Program.cs
```

Expected: the directory contains `MyApp.ServiceDefaults.csproj` and `Extensions.cs`, and `Program.cs` shows both call sites.

- [ ] **Step 4: Build and test the scaffolded project**

Run:

```bash
dotnet build /tmp/otel-check/MyApp/MyApp.slnx -warnaserror
cd /tmp/otel-check/MyApp && dotnet test
```

Expected: `Build succeeded.` with `0 Warning(s)`, and all tests pass — including the four new `ServiceDefaultsTests`, which in the generated project assert against `MyApp`'s own host.

- [ ] **Step 5: Confirm the feature sub-template still works alongside it**

Run:

```bash
dotnet new install /workspaces/modulith-template/working/content/feature
cd /tmp/otel-check/MyApp && dotnet new modulith-feature --appName MyApp -n Payments
dotnet build /tmp/otel-check/MyApp/MyApp.slnx -warnaserror
```

Expected: eight new projects, registered in the `.slnx`, and a clean build. This task changed nothing under `working/content/feature/`, so a failure here means Task 1's `.slnx` edit collided with the feature template's post-action — investigate rather than working around it.

- [ ] **Step 6: Clean up**

Run:

```bash
dotnet new uninstall /workspaces/modulith-template/working/content/modulith
dotnet new uninstall /workspaces/modulith-template/working/content/feature
rm -rf /tmp/otel-check
```

The research for this plan also left `Aspire.ProjectTemplates` installed globally. Remove it unless the maintainer wants it kept:

```bash
dotnet new uninstall Aspire.ProjectTemplates
```

- [ ] **Step 7: Final verification and checkpoint**

Run both gates one last time from a clean state:

```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && dotnet test
```

Expected: clean build, all tests pass.

Do not commit. Report to the maintainer: the files changed, the verification actually run, and — explicitly — that **no end-to-end run against a live OTLP endpoint was performed**, so the `.AddSource("Npgsql")` line is backed only by the static check in Task 1 Step 1. If that string is wrong in a way the static check missed, database spans are silently absent and nothing in the build or test suite would say so.

---

## Deviations from the spec

Recorded so review can catch them rather than discover them later.

1. **The spec's third smoke test — "no OTLP exporter is configured when `OTEL_EXPORTER_OTLP_ENDPOINT` is absent" — is not implemented.** OpenTelemetry exposes no clean way to observe the absence of an exporter from the service provider, so the assertion would have to reach into internals and would break on any package upgrade. Replaced by two tests covering `MapDefaultEndpoints`' Development-only behaviour, which pins the surprising part of the design and is observable through the public endpoint data sources.

2. **The spec's static Npgsql check reads the Npgsql assembly**; Task 1 Step 1 reads the `Npgsql.OpenTelemetry` source on GitHub instead. Same fact, and it yields the literal string directly rather than requiring decompilation.

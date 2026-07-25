# Per-Layer Test Projects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every feature — the `Orders` example and every feature scaffolded by `dotnet new modulith-feature` — four correctly-wired test projects, one per layer, each with a single meaningful smoke test.

**Architecture:** Test projects mirror `src/` under the existing root `test/` folder as `test/Features/<Name>/<App>.Features.<Name>.<Layer>Tests`. A new `test/Directory.Build.props` carries every setting and package the test projects share, so each individual `.csproj` collapses to a project reference. The existing `ModulithTemplate.ArchitectureTests` migrates onto the same shared props.

**Tech Stack:** .NET 10, Microsoft.Testing.Platform (runner, set in `global.json`), xUnit v3, NSubstitute, bUnit (Blazor component tests), central package management.

**Spec:** `docs/superpowers/specs/2026-07-25-per-layer-test-projects-design.md`

## Global Constraints

- **Do not perform any git actions.** Both `CLAUDE.md` files state this explicitly ("Do not perform any git actions unless explicitly asked — the maintainer handles commits, branches, and releases"). This overrides the writing-plans skill's default "commit after each task" step. Every task therefore ends at a verification command, not a commit. Leave changes in the working tree for the maintainer.
- **This repo is a `dotnet new` template package, not an application.** All work happens under `working/content/`. Paths below are relative to the repo root `/workspaces/modulith-template`.
- **`sourceName` is `ModulithTemplate`** for the solution template and **`FeatureName`** for the feature sub-template (which additionally replaces the `ModulithApp` token via a required `appName` parameter). Every project name, namespace, directory and identifier introduced under `working/content/modulith/` must keep the `ModulithTemplate` prefix; everything under `working/content/feature/` must use `ModulithApp` / `FeatureName`. An identifier that should be renamed per-project but omits the token leaks the template's name into generated projects.
- **The build must be warning-clean under `-warnaserror`.** CI runs `dotnet build --no-restore -warnaserror`. Test projects inherit Meziantou.Analyzer, SonarAnalyzer.CSharp and Roslynator.Analyzers from the root `Directory.Build.props`, and `EnforceCodeStyleInBuild` is on. Suppress narrowly with `#pragma warning disable <id>` plus a matching restore; never disable globally.
- **Central package management is in force.** Add versions to `working/content/modulith/Directory.Packages.props`; `PackageReference` elements in `.csproj` files carry **no** `Version` attribute.
- **Exact package versions** (only one is new — `AngleSharp`; the rest are already pinned):
  - `AngleSharp` → `1.5.2` (NEW, Task 5)
  - `bunit` → `2.7.2`, `xunit.v3` → `3.2.2`, `xunit.analyzers` → `1.27.0`, `NSubstitute` → `5.3.0`, `NSubstitute.Analyzers.CSharp` → `1.0.17`, `coverlet.collector` → `10.0.1`, `Microsoft.Testing.Extensions.CodeCoverage` → `17.14.2`
- **Test naming convention** (from the generated project's `CLAUDE.md`): class `<Thing>Tests`; method `<Method>_<conditions>_<asserted outcome>` with the leading token in its source casing and the remainder snake_case, never an `Async` suffix; Arrange / Act / Assert sections each carrying a comment.
- **XML doc comments are required on public classes and methods** per the same `CLAUDE.md`, and are included on every type below.

---

### Task 0: Restore a green baseline

The working tree does not currently build. An uncommitted edit to the Orders Infrastructure project removed a `ProjectReference` that its own `Configuration.cs` requires. Every verification step in every later task depends on a building solution, so this must be settled first.

**Files:**
- Modify: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj`

- [ ] **Step 1: Reproduce the failure**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
```

Expected: FAIL with
```
Configuration.cs(6,24): error CS0234: The type or namespace name 'Infrastructure' does not exist in the namespace 'ModulithTemplate'
```

- [ ] **Step 2: Confirm the cause with the maintainer**

`git diff` on that `.csproj` shows this line was deleted:

```xml
<ProjectReference Include="../../../ModulithTemplate.Infrastructure.Common/ModulithTemplate.Infrastructure.Common.csproj" />
```

`Configuration.cs` in the same project does `using ModulithTemplate.Infrastructure.Common;` and calls `AddModuleDbContext<OrdersContext>(configuration, schema: "orders")`, which lives in that project.

**Ask the maintainer which they want** before changing anything — this is their uncommitted work, and both readings are plausible:
- **(a)** the deletion was accidental → restore the `ProjectReference` line;
- **(b)** the deletion is intentional WIP toward removing the `Infrastructure.Common` dependency → then `Configuration.cs` needs a replacement for `AddModuleDbContext`, which is out of scope for this plan and must land first.

- [ ] **Step 3: Apply the agreed fix and verify green**

If (a), restore the item so the `ItemGroup` reads:

```xml
  <ItemGroup>
    <ProjectReference Include="../ModulithTemplate.Features.Orders.Domain/ModulithTemplate.Features.Orders.Domain.csproj" />
    <ProjectReference Include="../../../ModulithTemplate.FeatureCore/ModulithTemplate.FeatureCore.csproj" />
    <ProjectReference Include="../../../ModulithTemplate.Infrastructure.Common/ModulithTemplate.Infrastructure.Common.csproj" />
  </ItemGroup>
```

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` and all existing architecture tests pass. **Record the passing test count** — later tasks check it does not regress.

---

### Task 1: Shared test-project settings (`test/Directory.Build.props`)

Introduces the file that every test project — existing and future — inherits from, and migrates `ModulithTemplate.ArchitectureTests` onto it. Deliverable: identical build and test results with the boilerplate moved.

**Files:**
- Create: `working/content/modulith/test/Directory.Build.props`
- Modify: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/ModulithTemplate.ArchitectureTests.csproj`

**Interfaces:**
- Produces: every project under `working/content/modulith/test/` automatically gets `OutputType=Exe`, `UseMicrosoftTestingPlatformRunner=true`, `IsPackable=false`, package references to `xunit.v3` / `xunit.analyzers` / `coverlet.collector` / `Microsoft.Testing.Extensions.CodeCoverage` / `NSubstitute` / `NSubstitute.Analyzers.CSharp`, a global `using Xunit;`, and `xunit.runner.json` copied to the output directory. Tasks 3–5 rely on all of this and re-declare none of it.

- [ ] **Step 1: Create the shared props file**

Create `working/content/modulith/test/Directory.Build.props`:

```xml
<Project>

  <!-- MSBuild stops at the FIRST Directory.Build.props it finds walking up from a project,
       so this file must import the solution-root one explicitly. Without it every test
       project silently loses TargetFramework, Nullable, ImplicitUsings and the
       Meziantou/Sonar/Roslynator analyzers. -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.analyzers" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
    <!-- NSubstitute lives here rather than per-project: an analyzer for an unreferenced
         package is inert, so analyzing every test project only pays off if every test
         project can actually substitute. -->
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="NSubstitute.Analyzers.CSharp">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <!-- Resolved from this file's own directory, not a relative '../' prefix: the
       architecture tests sit at test/<Project>/ but the feature tests sit at
       test/Features/<Name>/<Project>/, so no single relative prefix works for both.
       Link forces it to land at the output root as plain 'xunit.runner.json'. -->
  <ItemGroup>
    <Content Include="$(MSBuildThisFileDirectory)xunit.runner.json"
             Link="xunit.runner.json"
             CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Strip the now-shared settings from the architecture tests project**

Replace the entire contents of `working/content/modulith/test/ModulithTemplate.ArchitectureTests/ModulithTemplate.ArchitectureTests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference every feature layer project (any feature, any layer) so the architecture
         tests always build and analyze them, and new features are picked up automatically
         with no edits here. Without this, the feature projects are orphans in the reference
         graph and only a full-solution build produces their assemblies. -->
    <ProjectReference Include="../../src/Features/**/*.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="TngTech.ArchUnitNET.xUnitV3" />
  </ItemGroup>

</Project>
```

Everything removed (`OutputType`, `UseMicrosoftTestingPlatformRunner`, the four test packages, the `Using`, the `xunit.runner.json` content item) now comes from the shared props.

- [ ] **Step 3: Verify the migration changed nothing observable**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: build clean; the same architecture tests pass, same count as recorded in Task 0.

- [ ] **Step 4: Verify `xunit.runner.json` still reaches the output**

Run:
```bash
ls working/content/modulith/test/ModulithTemplate.ArchitectureTests/bin/Debug/net10.0/xunit.runner.json
```

Expected: the file exists at the output **root** (not nested under a copied directory structure). If it is missing or nested, the `Link` metadata is wrong — fix it before continuing, because Tasks 3–5 depend on the same mechanism.

---

### Task 2: Stop the architecture tests from loading test assemblies

`SolutionAssemblies.LoadFeatureAssemblies` selects DLLs by testing the **full path** for `".Features."`. A directory named `ModulithTemplate.Features.Orders.WebTests` matches, so once Tasks 3–5 land, every DLL in those output folders — xunit, bUnit, AngleSharp, NSubstitute, Castle and the test assemblies themselves — would be loaded into the ArchUnitNET architecture. This must be fixed **before** the test projects are added.

**Files:**
- Modify: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/SolutionAssemblies.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `SolutionAssemblies.FeatureAssemblies` now contains **only** assemblies whose own file name contains `.Features.` and whose name does not end in `Tests`. `FeatureLayerTests` and `FeatureModuleTests` continue to consume it unchanged.

- [ ] **Step 1: Establish the current behaviour as a baseline**

Run:
```bash
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: PASS. Note the count — it must be identical after the change.

- [ ] **Step 2: Narrow the filter**

In `SolutionAssemblies.cs`, replace the body of `LoadFeatureAssemblies`:

```csharp
    private static Assembly[] LoadFeatureAssemblies()
    {
        try
        {
            return Directory.GetFiles(GetSolutionDirectory(), "*.dll", SearchOption.AllDirectories)
                // Match the assembly's own file name, not the full path: a test project
                // directory such as ModulithTemplate.Features.Orders.WebTests would otherwise
                // pull its entire output (xunit, bUnit, AngleSharp, NSubstitute, ...) into the
                // architecture. Assemblies ending in "Tests" are excluded for the same reason.
                .Where(f => Path.GetFileName(f).Contains(FeaturesKeyword, StringComparison.Ordinal)
                    && !Path.GetFileNameWithoutExtension(f).EndsWith("Tests", StringComparison.Ordinal))
                .DistinctBy(Path.GetFileName)
                .Select(Assembly.LoadFile)
                .ToArray();
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }
```

Note the comparison also changes from `InvariantCulture` to `Ordinal`, matching the rest of the file.

- [ ] **Step 3: Verify the filter did not over-narrow**

Run:
```bash
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: PASS with the same count as Step 1.

This is the meaningful check: `FeatureLayerTests.AssertLayerRule` asserts `layerPresent` and `FeatureModuleTests` asserts `Assert.NotEmpty(SolutionAssemblies.FeatureAssemblies)` precisely so a discovery regression fails loudly instead of passing vacuously. If those guards trip, the filter is too tight.

- [ ] **Step 4: Verify the build is still clean**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
```

Expected: `0 Warning(s) 0 Error(s)`.

---

### Task 3: Orders Domain and Application test projects

The two projects that need nothing beyond the shared props. Grouped because neither adds a package and both are proved by the same build-and-test cycle.

**Files:**
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.DomainTests/ModulithTemplate.Features.Orders.DomainTests.csproj`
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.DomainTests/OrdersDomainSmokeTests.cs`
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests/ModulithTemplate.Features.Orders.ApplicationTests.csproj`
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests/OrdersApplicationSmokeTests.cs`
- Modify: `working/content/modulith/ModulithTemplate.slnx`

**Interfaces:**
- Consumes: `test/Directory.Build.props` from Task 1 (supplies the SDK settings, xUnit, NSubstitute and the global `using Xunit;`).
- Produces: the `/test/Features/` and `/test/Features/Orders/` solution folders in `ModulithTemplate.slnx`, which Tasks 4 and 5 add to.

- [ ] **Step 1: Write the Domain smoke test**

Create `.../ModulithTemplate.Features.Orders.DomainTests/OrdersDomainSmokeTests.cs`:

```csharp
using ModulithTemplate.FeatureCore;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.DomainTests;

/// <summary>Smoke tests for the Orders domain layer.</summary>
public class OrdersDomainSmokeTests
{
    /// <summary>
    /// The feature-owned repository abstraction must extend the shared <see cref="IRepository{T}"/>.
    /// That relationship is what lets the feature's open-generic DI registration bind to its own
    /// <c>DbContext</c> instead of whichever feature registered last.
    /// </summary>
    [Fact]
    public void IOrdersRepository_for_a_domain_entity_extends_the_shared_IRepository()
    {
        // Arrange
        var featureRepository = typeof(IOrdersRepository<SomeEntity>);

        // Act
        var extendsSharedRepository = typeof(IRepository<SomeEntity>).IsAssignableFrom(featureRepository);

        // Assert
        Assert.True(extendsSharedRepository);
    }
}
```

- [ ] **Step 2: Write the Domain test project file**

Create `.../ModulithTemplate.Features.Orders.DomainTests/ModulithTemplate.Features.Orders.DomainTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/Orders/ModulithTemplate.Features.Orders.Domain/ModulithTemplate.Features.Orders.Domain.csproj" />
  </ItemGroup>

</Project>
```

Four `../` segments: `DomainTests/` → `Orders/` → `Features/` → `test/` → solution root.

- [ ] **Step 3: Write the Application smoke test**

Create `.../ModulithTemplate.Features.Orders.ApplicationTests/OrdersApplicationSmokeTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Application;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Smoke tests for the Orders application layer.</summary>
public class OrdersApplicationSmokeTests
{
    /// <summary>
    /// Smoke test for the layer's test toolchain: a substituted <see cref="IOrdersRepository{T}"/>
    /// is accepted by the container and <c>ConfigureOrdersApplication</c> composes onto it.
    /// Replace this with a real app-service test once the layer registers one.
    /// </summary>
    [Fact]
    public void ConfigureOrdersApplication_with_a_substituted_repository_builds_a_resolvable_provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IOrdersRepository<SomeEntity>>());

        // Act
        using var provider = services.ConfigureOrdersApplication().BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IOrdersRepository<SomeEntity>>());
    }
}
```

`Microsoft.Extensions.DependencyInjection` and the Domain types arrive transitively through the Application project reference.

- [ ] **Step 4: Write the Application test project file**

Create `.../ModulithTemplate.Features.Orders.ApplicationTests/ModulithTemplate.Features.Orders.ApplicationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/Orders/ModulithTemplate.Features.Orders.Application/ModulithTemplate.Features.Orders.Application.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Register both projects in the solution**

In `working/content/modulith/ModulithTemplate.slnx`, replace the existing `/test/` folder element with:

```xml
  <Folder Name="/test/">
    <Project Path="test/ModulithTemplate.ArchitectureTests/ModulithTemplate.ArchitectureTests.csproj" />
  </Folder>
  <Folder Name="/test/Features/" />
  <Folder Name="/test/Features/Orders/">
    <Project Path="test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests/ModulithTemplate.Features.Orders.ApplicationTests.csproj" />
    <Project Path="test/Features/Orders/ModulithTemplate.Features.Orders.DomainTests/ModulithTemplate.Features.Orders.DomainTests.csproj" />
  </Folder>
```

This mirrors how `/src/Features/` is declared as an empty folder followed by `/src/Features/Orders/` with its projects listed alphabetically.

- [ ] **Step 6: Run the two new tests**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: `0 Warning(s) 0 Error(s)`, and the architecture-test count from Task 0 **plus 2**.

If an analyzer fires on the test code, fix the code rather than the analyzer settings; `#pragma warning disable <id>` with a matching restore only when a rule genuinely does not apply.

---

### Task 4: Orders Infrastructure test project

Asserts the `HasDefaultSchema("orders")` wiring against the EF **model**, with no database contacted.

**Files:**
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.InfrastructureTests/ModulithTemplate.Features.Orders.InfrastructureTests.csproj`
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.InfrastructureTests/OrdersInfrastructureSmokeTests.cs`
- Modify: `working/content/modulith/ModulithTemplate.slnx`

**Interfaces:**
- Consumes: `test/Directory.Build.props` (Task 1); the `/test/Features/Orders/` solution folder (Task 3).

- [ ] **Step 1: Write the failing test**

Create `.../ModulithTemplate.Features.Orders.InfrastructureTests/OrdersInfrastructureSmokeTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

using ModulithTemplate.Features.Orders.Infrastructure.Data;

namespace ModulithTemplate.Features.Orders.InfrastructureTests;

/// <summary>Smoke tests for the Orders infrastructure layer.</summary>
public class OrdersInfrastructureSmokeTests
{
    /// <summary>
    /// Every feature owns its schema, set via <c>HasDefaultSchema</c> in <c>OnModelCreating</c>.
    /// Building the model is enough to assert it — Npgsql does not open a connection to do so,
    /// which keeps this a unit test with no database dependency.
    /// </summary>
    [Fact]
    public void OrdersContext_model_defaults_to_the_orders_schema()
    {
        // Arrange — a syntactically valid connection string is required; nothing connects to it.
        var options = new DbContextOptionsBuilder<OrdersContext>()
            .UseNpgsql("Host=localhost;Database=modulith_tests")
            .Options;

        // Act
        using var context = new OrdersContext(options);
        var defaultSchema = context.Model.GetDefaultSchema();

        // Assert
        Assert.Equal("orders", defaultSchema);
    }
}
```

- [ ] **Step 2: Write the project file**

Create `.../ModulithTemplate.Features.Orders.InfrastructureTests/ModulithTemplate.Features.Orders.InfrastructureTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/Orders/ModulithTemplate.Features.Orders.Infrastructure/ModulithTemplate.Features.Orders.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

`Microsoft.EntityFrameworkCore` and `UseNpgsql` flow in transitively — the Infrastructure project references `Ardalis.Specification.EntityFrameworkCore` and (per Task 0) `ModulithTemplate.Infrastructure.Common`, which references `Npgsql.EntityFrameworkCore.PostgreSQL`. If `UseNpgsql` does not resolve, Task 0 was resolved as option (b) and this task's premise needs revisiting.

- [ ] **Step 3: Register the project in the solution**

In `ModulithTemplate.slnx`, add to the `/test/Features/Orders/` folder, keeping alphabetical order:

```xml
    <Project Path="test/Features/Orders/ModulithTemplate.Features.Orders.InfrastructureTests/ModulithTemplate.Features.Orders.InfrastructureTests.csproj" />
```

Placed after `...DomainTests.csproj`.

- [ ] **Step 4: Run the test**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: clean build; Task 3's count **plus 1**. The test must complete in well under a second — if it hangs or reports a connection error, the model is being built against a live connection and the test needs rethinking, not a running Postgres.

---

### Task 5: Orders Web test project (bUnit) and the `AngleSharp` pin

bUnit is the one addition that breaks the build before it fixes it. This task deliberately reproduces that failure first.

**Files:**
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.WebTests/ModulithTemplate.Features.Orders.WebTests.csproj`
- Create: `working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.WebTests/OrdersWebSmokeTests.cs`
- Modify: `working/content/modulith/Directory.Packages.props`
- Modify: `working/content/modulith/ModulithTemplate.slnx`

**Interfaces:**
- Consumes: `test/Directory.Build.props` (Task 1); the `/test/Features/Orders/` solution folder (Task 3).
- Produces: the `AngleSharp` `PackageVersion` pin, which Task 6's scaffolded `WebTests` project also depends on (it inherits the generated solution's `Directory.Packages.props`).

- [ ] **Step 1: Write the smoke test**

Create `.../ModulithTemplate.Features.Orders.WebTests/OrdersWebSmokeTests.cs`:

```csharp
using Bunit;

using Microsoft.AspNetCore.Components;

namespace ModulithTemplate.Features.Orders.WebTests;

/// <summary>Smoke tests for the Orders web layer.</summary>
public class OrdersWebSmokeTests
{
    /// <summary>
    /// Verifies the bUnit rendering pipeline is wired up, so feature components added later can
    /// be rendered and asserted against with <c>MarkupMatches</c>.
    /// </summary>
    [Fact]
    public void BunitContext_renders_a_render_fragment_to_markup()
    {
        // Arrange
        using var context = new BunitContext();
        RenderFragment fragment = builder =>
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, "orders");
            builder.CloseElement();
        };

        // Act
        var rendered = context.Render(fragment);

        // Assert
        rendered.MarkupMatches("<p>orders</p>");
    }
}
```

The lambda's `builder` parameter is inferred as `RenderTreeBuilder`; inferred lambda parameters need no `using` for their type, so `Microsoft.AspNetCore.Components.Rendering` is not imported.

- [ ] **Step 2: Write the project file**

Create `.../ModulithTemplate.Features.Orders.WebTests/ModulithTemplate.Features.Orders.WebTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <ItemGroup>
    <PackageReference Include="bunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/Orders/ModulithTemplate.Features.Orders.Web/ModulithTemplate.Features.Orders.Web.csproj" />
  </ItemGroup>

</Project>
```

The Razor SDK is used so a developer can drop `.razor` test components in later without changing the SDK.

- [ ] **Step 3: Register the project in the solution**

In `ModulithTemplate.slnx`, add to `/test/Features/Orders/` after `...InfrastructureTests.csproj`:

```xml
    <Project Path="test/Features/Orders/ModulithTemplate.Features.Orders.WebTests/ModulithTemplate.Features.Orders.WebTests.csproj" />
```

- [ ] **Step 4: Reproduce the NU1902 build failure**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
```

Expected: FAIL with
```
error NU1902: Package 'AngleSharp' 1.4.0 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-pgww-w46g-26qg
```

bUnit 2.7.2 pulls AngleSharp 1.4.0 transitively; NuGet audit raises `NU1902`, and `-warnaserror` promotes it to an error. Seeing this failure is the point of the step — it is what both this repo's CI and every generated project's CI would hit.

- [ ] **Step 5: Pin AngleSharp**

Two edits to `working/content/modulith/Directory.Packages.props`.

First, enable transitive pinning in the existing `PropertyGroup`:

```xml
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <!-- Lets a PackageVersion govern a transitive dependency with no direct PackageReference.
         Required for the AngleSharp pin below; without it the pin is silently ignored. -->
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
```

Second, add as the **first** entry of the `ItemGroup` (the list is alphabetical, and `AngleSharp` sorts before `Ardalis.Specification.EntityFrameworkCore`):

```xml
    <!-- Transitive dependency of bunit. Pinned above the version bunit resolves (1.4.0) to
         clear NU1902 (GHSA-pgww-w46g-26qg), which -warnaserror turns into a build error. -->
    <PackageVersion Include="AngleSharp" Version="1.5.2" />
```

**Both edits are required.** Under central package management a `PackageVersion` alone governs only *direct* `PackageReference`s; transitive dependencies keep whatever version the graph resolves unless `CentralPackageTransitivePinningEnabled` is set. This is a solution-wide behaviour change: every `PackageVersion` entry now also governs transitives.

- [ ] **Step 6: Verify the build is clean and the test passes**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: `0 Warning(s) 0 Error(s)`, no `NU1902`, and Task 4's count **plus 1** — four new smoke tests in total on top of the architecture tests.

---

### Task 6: Feature sub-template scaffolds the four test projects

`working/content/feature/` grows a mirrored `test/Features/FeatureName/` tree and `template.json` registers all eight projects. Ends with the full end-to-end scaffold verification, which is the real gate for this task.

**Files:**
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.DomainTests/ModulithApp.Features.FeatureName.DomainTests.csproj`
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.DomainTests/FeatureNameDomainSmokeTests.cs`
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.ApplicationTests/ModulithApp.Features.FeatureName.ApplicationTests.csproj`
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.ApplicationTests/FeatureNameApplicationSmokeTests.cs`
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.InfrastructureTests/ModulithApp.Features.FeatureName.InfrastructureTests.csproj`
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.InfrastructureTests/FeatureNameInfrastructureSmokeTests.cs`
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.WebTests/ModulithApp.Features.FeatureName.WebTests.csproj`
- Create: `working/content/feature/test/Features/FeatureName/ModulithApp.Features.FeatureName.WebTests/FeatureNameWebSmokeTests.cs`
- Modify: `working/content/feature/.template.config/template.json`

**Interfaces:**
- Consumes: the scaffolded projects land in an already-generated solution, so they inherit that solution's `test/Directory.Build.props` (Task 1), `test/xunit.runner.json`, and `Directory.Packages.props` including the `AngleSharp` pin (Task 5). None of that is duplicated here.

**Key difference from Orders:** a scaffolded feature has **no entity** — `SomeEntity` exists only in the `Orders` example. The Domain and Application smoke tests therefore declare their own `TestEntity`.

- [ ] **Step 1: Write the Domain test project**

Create `.../ModulithApp.Features.FeatureName.DomainTests/FeatureNameDomainSmokeTests.cs`:

```csharp
using ModulithApp.FeatureCore;
using ModulithApp.Features.FeatureName.Domain;

namespace ModulithApp.Features.FeatureName.DomainTests;

/// <summary>Smoke tests for the FeatureName domain layer.</summary>
public class FeatureNameDomainSmokeTests
{
    /// <summary>Stand-in entity; replace with a real domain entity once the feature has one.</summary>
#pragma warning disable S2094 // Classes should not be empty
    private sealed class TestEntity;
#pragma warning restore S2094 // Classes should not be empty

    /// <summary>
    /// The feature-owned repository abstraction must extend the shared <see cref="IRepository{T}"/>.
    /// That relationship is what lets the feature's open-generic DI registration bind to its own
    /// <c>DbContext</c> instead of whichever feature registered last.
    /// </summary>
    [Fact]
    public void IFeatureNameRepository_for_a_domain_entity_extends_the_shared_IRepository()
    {
        // Arrange
        var featureRepository = typeof(IFeatureNameRepository<TestEntity>);

        // Act
        var extendsSharedRepository = typeof(IRepository<TestEntity>).IsAssignableFrom(featureRepository);

        // Assert
        Assert.True(extendsSharedRepository);
    }
}
```

Create `.../ModulithApp.Features.FeatureName.DomainTests/ModulithApp.Features.FeatureName.DomainTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/FeatureName/ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the Application test project**

Create `.../ModulithApp.Features.FeatureName.ApplicationTests/FeatureNameApplicationSmokeTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

using ModulithApp.Features.FeatureName.Application;
using ModulithApp.Features.FeatureName.Domain;

using NSubstitute;

namespace ModulithApp.Features.FeatureName.ApplicationTests;

/// <summary>Smoke tests for the FeatureName application layer.</summary>
public class FeatureNameApplicationSmokeTests
{
    /// <summary>
    /// Stand-in entity; replace with a real domain entity once the feature has one. Must be
    /// visible outside this assembly (not <c>private</c>) because NSubstitute's Castle proxy
    /// generator needs access to it as a generic argument when substituting
    /// <see cref="IFeatureNameRepository{T}"/> below.
    /// </summary>
#pragma warning disable S2094 // Classes should not be empty
    public sealed class TestEntity;
#pragma warning restore S2094 // Classes should not be empty

    /// <summary>
    /// Smoke test for the layer's test toolchain: a substituted <see cref="IFeatureNameRepository{T}"/>
    /// is accepted by the container and <c>ConfigureFeatureNameApplication</c> composes onto it.
    /// Replace this with a real app-service test once the layer registers one.
    /// </summary>
    [Fact]
    public void ConfigureFeatureNameApplication_with_a_substituted_repository_builds_a_resolvable_provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IFeatureNameRepository<TestEntity>>());

        // Act
        using var provider = services.ConfigureFeatureNameApplication().BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IFeatureNameRepository<TestEntity>>());
    }
}
```

Create `.../ModulithApp.Features.FeatureName.ApplicationTests/ModulithApp.Features.FeatureName.ApplicationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the Infrastructure test project**

Create `.../ModulithApp.Features.FeatureName.InfrastructureTests/FeatureNameInfrastructureSmokeTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

using ModulithApp.Features.FeatureName.Infrastructure.Data;

namespace ModulithApp.Features.FeatureName.InfrastructureTests;

/// <summary>Smoke tests for the FeatureName infrastructure layer.</summary>
public class FeatureNameInfrastructureSmokeTests
{
    /// <summary>
    /// Every feature owns its schema, set via <c>HasDefaultSchema</c> in <c>OnModelCreating</c>.
    /// Building the model is enough to assert it — Npgsql does not open a connection to do so,
    /// which keeps this a unit test with no database dependency.
    /// </summary>
    [Fact]
    public void FeatureNameContext_model_defaults_to_the_feature_schema()
    {
        // Arrange — a syntactically valid connection string is required; nothing connects to it.
        var options = new DbContextOptionsBuilder<FeatureNameContext>()
            .UseNpgsql("Host=localhost;Database=modulith_tests")
            .Options;

        // Act
        using var context = new FeatureNameContext(options);
        var defaultSchema = context.Model.GetDefaultSchema();

        // Assert
        Assert.Equal("featureschema", defaultSchema);
    }
}
```

`"featureschema"` is the token the template's `schemaName` symbol replaces with the lower-cased feature name — the same token already used in `FeatureNameContext.cs` and `Configuration.cs`. Do **not** hand-write the feature name here.

Create `.../ModulithApp.Features.FeatureName.InfrastructureTests/ModulithApp.Features.FeatureName.InfrastructureTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/FeatureName/ModulithApp.Features.FeatureName.Infrastructure/ModulithApp.Features.FeatureName.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Write the Web test project**

Create `.../ModulithApp.Features.FeatureName.WebTests/FeatureNameWebSmokeTests.cs`:

```csharp
using Bunit;

using Microsoft.AspNetCore.Components;

namespace ModulithApp.Features.FeatureName.WebTests;

/// <summary>Smoke tests for the FeatureName web layer.</summary>
public class FeatureNameWebSmokeTests
{
    /// <summary>
    /// Verifies the bUnit rendering pipeline is wired up, so feature components added later can
    /// be rendered and asserted against with <c>MarkupMatches</c>.
    /// </summary>
    [Fact]
    public void BunitContext_renders_a_render_fragment_to_markup()
    {
        // Arrange
        using var context = new BunitContext();
        RenderFragment fragment = builder =>
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, "feature");
            builder.CloseElement();
        };

        // Act
        var rendered = context.Render(fragment);

        // Assert
        rendered.MarkupMatches("<p>feature</p>");
    }
}
```

Create `.../ModulithApp.Features.FeatureName.WebTests/ModulithApp.Features.FeatureName.WebTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <ItemGroup>
    <PackageReference Include="bunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../../../src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/ModulithApp.Features.FeatureName.Web.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Register all eight projects in `template.json`**

In `working/content/feature/.template.config/template.json`, replace `primaryOutputs` and the post-action's `primaryOutputIndexes`:

```json
  "primaryOutputs": [
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj" },
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj" },
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Infrastructure/ModulithApp.Features.FeatureName.Infrastructure.csproj" },
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/ModulithApp.Features.FeatureName.Web.csproj" },
    { "path": "test/Features/FeatureName/ModulithApp.Features.FeatureName.DomainTests/ModulithApp.Features.FeatureName.DomainTests.csproj" },
    { "path": "test/Features/FeatureName/ModulithApp.Features.FeatureName.ApplicationTests/ModulithApp.Features.FeatureName.ApplicationTests.csproj" },
    { "path": "test/Features/FeatureName/ModulithApp.Features.FeatureName.InfrastructureTests/ModulithApp.Features.FeatureName.InfrastructureTests.csproj" },
    { "path": "test/Features/FeatureName/ModulithApp.Features.FeatureName.WebTests/ModulithApp.Features.FeatureName.WebTests.csproj" }
  ],
  "postActions": [
    {
      "id": "addProjectsToSolution",
      "description": "Add projects to solution",
      "manualInstructions": [
        { "text": "Add the generated .csproj files to the solution manually with `dotnet sln add`" }
      ],
      "actionId": "D396686C-DE0E-4DE6-906D-291CD29FC5DE",
      "continueOnError": true,
      "args": {
        "primaryOutputIndexes": "0;1;2;3;4;5;6;7"
      }
    }
  ]
```

Leave `$schema`, `author`, `classifications`, `identity`, `name`, `shortName`, `sourceName`, `preferNameDirectory`, `tags` and `symbols` untouched.

- [ ] **Step 6: Scaffold a solution and a feature into it, end to end**

Run:
```bash
rm -rf /tmp/MyApp
dotnet new install working/content/modulith
dotnet new install working/content/feature
dotnet new modulith -n MyApp -o /tmp/MyApp
cd /tmp/MyApp && dotnet new modulith-feature --appName MyApp -n Payments
```

Expected: eight `Payments` projects created and the post-action reports success.

- [ ] **Step 7: Verify the generated solution builds and tests clean**

Run (from `/tmp/MyApp`):
```bash
dotnet build -warnaserror
dotnet test
```

Expected: `0 Warning(s) 0 Error(s)`, no `NU1902`, and **eight** smoke tests pass (four from `Orders`, four from `Payments`) alongside the architecture tests.

- [ ] **Step 8: Verify solution registration and that no template tokens leaked**

Run (from `/tmp/MyApp`):
```bash
grep -c "test/Features/Payments" MyApp.slnx
grep -rIl -e ModulithTemplate -e FeatureName -e ModulithApp . \
  --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git
find . -path ./bin -prune -o -path ./obj -prune -o \
  \( -name '*ModulithTemplate*' -o -name '*FeatureName*' -o -name '*ModulithApp*' \) -print
```

Expected: the first command prints `5` — four `<Project>` entries plus the `<Folder Name="/test/Features/Payments/">` element, which matches the same substring. The second and third print **nothing**; any hit is a token that failed to rename and must be fixed before this task is done.

- [ ] **Step 9: Clean up the installed templates**

Run:
```bash
cd /workspaces/modulith-template
dotnet new uninstall working/content/modulith
dotnet new uninstall working/content/feature
rm -rf /tmp/MyApp
```

---

### Task 7: Correct the documentation

The generated project's `CLAUDE.md` currently documents test projects that have never existed. Bring it in line with what Tasks 1–6 actually built.

**Files:**
- Modify: `working/content/modulith/CLAUDE.md`
- Modify: `CLAUDE.md` (repo root)

- [ ] **Step 1: Fix the `dotnet test` example paths**

In `working/content/modulith/CLAUDE.md`, in the `## Commands` block, replace:

```bash
dotnet test --no-restore tests/ModulithTemplate.ApplicationTests          # one project
dotnet test --no-restore --filter-class "*SomwAppServiceTests*"           # one class
```

with:

```bash
dotnet test --project test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests   # one project
dotnet test --filter-class "*OrdersApplicationSmokeTests*"                                     # one class
```

Three defects are being corrected: the directory is `test/`, not `tests/`; the project name was wrong; and Microsoft.Testing.Platform needs `--project` rather than a bare path (a bare path is forwarded to the test app, which reports "Zero tests ran").

- [ ] **Step 2: Fix the Tests convention bullet**

In the `## Conventions` section, replace the `**Tests**` bullet with:

```markdown
- **Tests**: xUnit v3 on Microsoft.Testing.Platform, **NSubstitute** for substitutes, **bUnit** for Blazor component tests. Each feature owns four test projects mirroring its layers at `test/Features/<Name>/<App>.Features.<Name>.{Domain,Application,Infrastructure,Web}Tests`, scaffolded together with the feature by `dotnet new modulith-feature`. Shared settings and the common test packages (xUnit, NSubstitute, coverage) come from `test/Directory.Build.props`, so an individual test `.csproj` normally holds nothing but a `ProjectReference`. Test method names are snake_case describing behavior.
```

The `TestFactory.CreateUnitOfWorkSubstitute` reference is deleted outright — no such helper exists or is planned.

- [ ] **Step 3: Fix the `<Name>.DomainTest` reference**

In the `### Where business rules live` section, change:

> cover it with a `<Name>.DomainTest` test on the entity

to:

> cover it with a `<Name>.DomainTests` test on the entity

- [ ] **Step 4: Document the layout in the Architecture section**

At the end of the `## Architecture` intro paragraph (after the sentence ending "domain driven design (DDD) architecture principles."), add:

```markdown
Each feature's four layer projects are mirrored by four test projects under `test/Features/<Name>/`, one per layer. `src/` holds production code only.
```

- [ ] **Step 5: Update "Adding a feature" for the eight-project result**

In the `## Adding a feature` section, change the sentence

> It creates `src/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}` and adds all four to the solution under a `/src/Features/Payments/` folder.

to

> It creates `src/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}` plus the matching `test/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}Tests`, and adds all eight to the solution under `/src/Features/Payments/` and `/test/Features/Payments/` folders.

Then append to the bulleted list describing the scaffold:

```markdown
- `test/Features/Payments/…{Domain,Application,Infrastructure,Web}Tests` — one test project per layer, each with a single smoke test to replace with real ones.
```

- [ ] **Step 6: Update the root `CLAUDE.md`**

In `## Template mechanics`, in the paragraph describing `modulith-feature`, change

> It scaffolds one feature's four layer projects (`Domain`/`Application`/`Infrastructure`/`Web`) into an *already-generated* solution

to

> It scaffolds one feature's four layer projects (`Domain`/`Application`/`Infrastructure`/`Web`) plus their four matching test projects under `test/Features/<Name>/` — **eight projects** — into an *already-generated* solution

- [ ] **Step 7: Verify the documented commands actually work**

Run:
```bash
dotnet test --project working/content/modulith/test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests
```

Expected: 1 test passes. Any command written into `CLAUDE.md` must be one that runs — the defects fixed in Step 1 are exactly what happens when it is not checked.

- [ ] **Step 8: Final full verification**

Run:
```bash
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
dotnet test working/content/modulith/ModulithTemplate.slnx
```

Expected: `0 Warning(s) 0 Error(s)`; the architecture tests plus four smoke tests all pass.

Leave every change in the working tree — per the Global Constraints, the maintainer handles the commit.

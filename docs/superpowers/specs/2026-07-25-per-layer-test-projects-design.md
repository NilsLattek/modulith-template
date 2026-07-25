# Design: per-layer test projects for every feature

**Date:** 2026-07-25
**Status:** Approved (design). Toolchain feasibility verified with a throwaway probe project
on .NET SDK 10 — bUnit 2.7.2 + xUnit v3 3.2.2 + Microsoft.Testing.Platform build and pass;
the `NU1902` blocker described below was reproduced and its fix verified.

## Problem

The template ships no unit tests. The only test project is
`ModulithTemplate.ArchitectureTests`, which asserts layering rules across features but never
exercises a single line of feature behaviour. A developer scaffolding a solution — or a new
feature via `dotnet new modulith-feature` — gets no place to put tests and no worked example
of the intended toolchain, so the first test written in a generated project is also the first
time anyone decides how tests are wired.

Two symptoms of that gap:

- `Directory.Packages.props` already pins `xunit.v3`, `xunit.analyzers`, `bunit`,
  `NSubstitute`, `NSubstitute.Analyzers.CSharp`, `coverlet.collector` and
  `Microsoft.Testing.Extensions.CodeCoverage` — versions staged for test projects that
  do not exist.
- The generated project's `CLAUDE.md` documents test projects named
  `ModulithTemplate.ApplicationTest`, `ModulithTemplate.WebTest` and `<Name>.DomainTest`,
  and a `TestFactory.CreateUnitOfWorkSubstitute` helper. **None of these have ever
  existed.** The documentation describes an imagined solution.

## Goal

Every feature — the `Orders` example in the solution template, and every feature scaffolded
by `dotnet new modulith-feature` — owns four test projects, one per layer, correctly wired to
the intended toolchain: **Microsoft.Testing.Platform** as runner, **xUnit v3** as framework,
**NSubstitute** for substitutes, **bUnit** for Blazor component tests.

## Non-goals (deliberately excluded)

- **Rich example tests.** The test projects are *skeletons*: correctly wired, with one
  meaningful smoke test each. The `Orders` feature stays as it is — `SomeEntity` remains an
  empty class, there is no app service and no Blazor component — so there is nothing yet to
  write real tests against. Fleshing out `Orders` into a worked example is a separate change.
- **A shared test-helper project** (`TestCommon`, `TestFactory`, builders). Nothing to share
  until there are real tests.
- **Database-backed integration tests.** No Testcontainers, no Respawn, no live Postgres in
  the test path. The Infrastructure smoke test asserts against the EF **model** only.
- **Code-coverage thresholds or reporting.** The coverage packages come along via the shared
  props because they are already pinned; no gate is configured.

## Layout

Test projects mirror `src/` under the existing root `test/` folder:

```
test/
  xunit.runner.json
  Directory.Build.props                      # NEW — shared test-project settings
  ModulithTemplate.ArchitectureTests/        # existing; simplified by the new props
  Features/
    Orders/
      ModulithTemplate.Features.Orders.DomainTests/
      ModulithTemplate.Features.Orders.ApplicationTests/
      ModulithTemplate.Features.Orders.InfrastructureTests/
      ModulithTemplate.Features.Orders.WebTests/
```

`src/` stays purely production code. This matters concretely: `ModulithTemplate.ArchitectureTests`
globs `../../src/Features/**/*.csproj` to pick up every feature layer project automatically, and
colocating test projects under `src/Features/<Name>/` would sweep them into that glob and require
an exclusion. Keeping tests under `test/` avoids inventing that rule.

The `Features/` level is retained (rather than a flat `test/<Name>/`) so the mirror of `src/`
holds as non-feature test projects are added.

### `test/Directory.Build.props`

Imports the root props via `$([MSBuild]::GetPathOfFileAbove())` — MSBuild stops at the first
`Directory.Build.props` it finds walking up, so without the explicit import the test projects
would silently lose `TargetFramework`, `Nullable`, and the Meziantou/Sonar/Roslynator analyzers.

It then carries everything every test project needs:

| Setting | Value |
|---|---|
| `OutputType` | `Exe` |
| `UseMicrosoftTestingPlatformRunner` | `true` |
| `IsPackable` | `false` |
| Packages | `xunit.v3`, `xunit.analyzers`, `coverlet.collector`, `Microsoft.Testing.Extensions.CodeCoverage`, `NSubstitute`, `NSubstitute.Analyzers.CSharp` |
| Usings | `<Using Include="Xunit" />` |
| Content | link to `test/xunit.runner.json`, `CopyToOutputDirectory=PreserveNewest` |

`NSubstitute.Analyzers.CSharp` is referenced with `PrivateAssets=all` /
`IncludeAssets="runtime; build; native; contentfiles; analyzers; buildtransitive"`, matching how
the root props wires the other analyzers.

**`NSubstitute` itself lives here too, not per-project.** An analyzer for an unreferenced package
is inert, so "every test project is analyzed" only pays off if every test project can actually
substitute. Infrastructure and Web tests are expected to need substitutes as the template grows.

The `xunit.runner.json` link replaces `ModulithTemplate.ArchitectureTests`' own
`<Content Include="../xunit.runner.json" />`; the relative depth differs between `test/X/` and
`test/Features/<Name>/X/`, so the shared props resolves it from `$(MSBuildThisFileDirectory)`
rather than a fixed `../` prefix.

### Per-project content

Each project contains exactly one file, `<Layer>SmokeTests.cs`, and a `.csproj` reduced to a
project reference plus — in one case — bUnit:

| Project | SDK | Adds beyond the shared props |
|---|---|---|
| `…Orders.DomainTests` | `Microsoft.NET.Sdk` | ref `Orders.Domain` |
| `…Orders.ApplicationTests` | `Microsoft.NET.Sdk` | ref `Orders.Application` |
| `…Orders.InfrastructureTests` | `Microsoft.NET.Sdk` | ref `Orders.Infrastructure` |
| `…Orders.WebTests` | `Microsoft.NET.Sdk.Razor` | ref `Orders.Web`, `bunit` |

`WebTests` uses the Razor SDK so a developer can add `.razor` test components later without
changing the SDK; the smoke test itself does not need one.

### The four smoke tests

Each proves its layer's toolchain works end to end, with no database and no web host:

- **DomainTests** — `IOrdersRepository<SomeEntity>` is assignable to `IRepository<SomeEntity>`.
  Guards the feature-owned-repository convention that the architecture depends on.
- **ApplicationTests** — `new ServiceCollection().ConfigureOrdersApplication()` succeeds, and an
  `NSubstitute.Substitute.For<IOrdersRepository<SomeEntity>>()` is created and used. Proves the
  DI entry point and the substitute toolchain.
- **InfrastructureTests** — an `OrdersContext` built over
  `new DbContextOptionsBuilder<OrdersContext>().UseNpgsql(<connection string>)` has
  `Model.GetDefaultSchema() == "orders"`. **No database is contacted** — Npgsql does not connect
  while building the model — so this asserts the `HasDefaultSchema` wiring in a pure unit test.
- **WebTests** — `BunitContext.Render(...)` renders a raw render fragment and
  `MarkupMatches` asserts the output. Proves bUnit is wired.

Test classes follow the generated project's own convention: `<Layer>SmokeTests`, methods named
`<Method>_<Conditions>_<AssertedOutcome>`, Arrange/Act/Assert commented.

Because test projects inherit Meziantou, SonarAnalyzer and Roslynator from the root props and CI
builds `-warnaserror`, the smoke tests must be analyzer-clean; narrow `#pragma warning disable`
with a matching restore is the escape hatch, consistent with existing suppressions in the template.

## Blocker found and resolved: `NU1902` fails the build

bUnit 2.7.2 pulls **AngleSharp 1.4.0** transitively, which carries a published moderate-severity
advisory (`GHSA-pgww-w46g-26qg`). NuGet audit raises `NU1902`, and **`dotnet build -warnaserror`
promotes it to an error** — which is exactly what both this repo's CI and the generated project's
CI run. Adding bUnit naively therefore breaks the build outright. Reproduced:

```
error NU1902: Package 'AngleSharp' 1.4.0 has a known moderate severity vulnerability
Build FAILED.
```

**Fix:** pin `<PackageVersion Include="AngleSharp" Version="1.5.2" />` in
`Directory.Packages.props`. Under central package management this raises the transitive
dependency without a direct `PackageReference` in any project. Verified: clean `-warnaserror`
build, bUnit test still passes.

This is the **only** package-version addition. Everything else the design needs is already pinned.

## Feature sub-template (`working/content/feature/`)

The sub-template gains four test projects mirroring the above, under
`test/Features/FeatureName/ModulithApp.Features.FeatureName.{Domain,Application,Infrastructure,Web}Tests`.
They scaffold into an already-generated solution, which by then already has
`test/Directory.Build.props` and `test/xunit.runner.json` from the solution template — so each
scaffolded `.csproj` is one `ProjectReference` (plus `bunit` for `WebTests`).

`template.json` changes:

- `primaryOutputs` grows from 4 entries to 8 (the four layer projects, then the four test projects).
- the `addProjectsToSolution` post-action's `primaryOutputIndexes` goes from `"0;1;2;3"` to
  `"0;1;2;3;4;5;6;7"`, so all eight are registered in the `.slnx`. Solution-folder placement is
  derived from each project's path, so the test projects land under `/test/Features/<Name>/`
  automatically, matching how the existing four land under `/src/Features/<Name>/`.

A scaffolded feature has **no entity** (`SomeEntity` exists only in `Orders`), so its Domain and
Application smoke tests declare a `private sealed class TestEntity` in the test project and use
`IFeatureNameRepository<TestEntity>`. Its Infrastructure smoke test asserts the schema token the
template already substitutes (`featureschema`).

Scaffolding a feature therefore emits **8 projects, not 4** — worth stating explicitly in the root
`CLAUDE.md` verification steps, since the existing instructions describe a 4-project result.

## Collateral fix: architecture-test assembly discovery

`SolutionAssemblies.LoadFeatureAssemblies` selects assemblies by testing whether the **full path**
of each discovered `*.dll` contains `".Features."`:

```csharp
Directory.GetFiles(GetSolutionDirectory(), "*.dll", SearchOption.AllDirectories)
    .Where(f => f.Contains(FeaturesKeyword, StringComparison.InvariantCulture))
```

A directory named `ModulithTemplate.Features.Orders.WebTests` contains `.Features.`, so **every**
DLL in that project's output — xunit, bUnit, AngleSharp, NSubstitute, Castle, and the test assembly
itself — would be `Assembly.LoadFile`d and fed into the ArchUnitNET architecture. That is slow and
semantically wrong: the architecture is meant to describe the feature layers, not the test
toolchain.

The bug predates this change (feature `bin` folders already contribute Npgsql, EF Core and Ardalis
assemblies), but adding test projects makes it materially worse.

**Fix:** match on the assembly **file name** rather than the full path, and exclude test assemblies:

```csharp
.Where(f => Path.GetFileName(f).Contains(FeaturesKeyword, StringComparison.Ordinal)
         && !Path.GetFileNameWithoutExtension(f).EndsWith("Tests", StringComparison.Ordinal))
```

The existing layering rules must still pass unchanged afterwards, including their
"fail loudly if no assembly was discovered" guards — that is the check that the narrowed filter
did not over-narrow.

## Documentation to correct

The generated project's `CLAUDE.md` (`working/content/modulith/CLAUDE.md`) currently describes
test projects that do not exist. Bring it in line with what is actually built:

- **Conventions → Tests**: replace `ModulithTemplate.WebTest` / `ModulithTemplate.ApplicationTest`
  with the real `<App>.Features.<Name>.{Domain,Application,Infrastructure,Web}Tests` names, and
  **delete the `TestFactory.CreateUnitOfWorkSubstitute` reference** — no such helper exists or is
  planned here.
- **Where business rules live**: `<Name>.DomainTest` → `<Name>.DomainTests`.
- **Commands**: the `dotnet test` example path `tests/ModulithTemplate.ApplicationTests` is wrong
  twice over (`tests/` vs `test/`, and the project name) — correct it to a real path.
- **Architecture / Adding a feature**: document the `test/Features/<Name>/` layout and that the
  sub-template now emits eight projects.

The root `CLAUDE.md` needs only the 8-projects note in the sub-template verification steps.

CI needs **no change**: `dotnet test` runs whatever the `.slnx` contains, and the new projects are
registered there.

## Verification

1. `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror` — clean, proving the
   `AngleSharp` pin holds and the smoke tests are analyzer-clean.
2. `dotnet test working/content/modulith/ModulithTemplate.slnx` — all architecture tests still pass
   (the narrowed assembly filter did not break them) and the four new smoke tests pass.
3. Scaffold end to end:
   ```bash
   dotnet new install working/content/modulith
   dotnet new modulith -n MyApp -o /tmp/MyApp
   dotnet new install working/content/feature
   cd /tmp/MyApp && dotnet new modulith-feature --appName MyApp -n Payments
   dotnet build -warnaserror && dotnet test
   ```
   Confirm: eight `Payments` projects in the `.slnx` (four under `/src/Features/Payments/`, four
   under `/test/Features/Payments/`), and **no file, directory, namespace or identifier named
   `ModulithTemplate`, `FeatureName` or `ModulithApp` survives** anywhere in the output.
4. `dotnet new uninstall` both templates.

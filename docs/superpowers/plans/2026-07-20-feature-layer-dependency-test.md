# Feature Layer Dependency Test Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a generic ArchUnitNET test that enforces the intra-feature layering rules (Domain depends on nothing; Application and Infrastructure depend only on Domain; Web may use Application/Infrastructure but not Domain directly), covering any number of features automatically.

**Architecture:** Layers are identified by **assembly name** (= project name, e.g. `ModulithTemplate.Features.Orders.Domain`), not namespace — because the Domain project ships a placeholder type in `namespace ModulithTemplate.Features.Example` that has no `.Domain` suffix, so namespace matching would silently miss the Domain layer. Assembly discovery/loading currently inline in `FeatureModuleTests` is extracted into a shared `SolutionAssemblies` helper; a new `FeatureLayerTests` uses it plus ArchUnitNET's `ResideInAssemblyMatching` / `NotDependOnAnyTypesThat` to assert forbidden dependencies are absent.

**Tech Stack:** C# / net10.0, xUnit v3, Microsoft.Testing.Platform runner, `TngTech.ArchUnitNET.xUnitV3` 0.13.3.

## Global Constraints

- The whole content solution builds with `-warnaserror`; Meziantou, SonarAnalyzer, and Roslynator run on build (`EnforceCodeStyleInBuild=true`). **Do not use `System.Text.RegularExpressions` in test code** — `new Regex`/`Regex.IsMatch` without a timeout triggers Meziantou MA0009 and fails the build. ArchUnitNET's own regex matching (inside `ResideInAssemblyMatching`) is fine; that is library-internal.
- All identifiers, project names, namespaces, and file paths under `working/content/modulith/` must keep the `ModulithTemplate` prefix (template `sourceName`). Do not introduce a non-prefixed project/namespace name.
- This repository's policy: **the assistant performs no git actions** (`CLAUDE.md`). "Checkpoint" steps below run build+test as the acceptance gate; committing is left to the maintainer.
- Test method names are snake_case describing behavior; no `Async` suffix.
- Build/test commands operate on the content solution:
  - Build: `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror`
  - Test all: `dotnet test working/content/modulith/ModulithTemplate.slnx`
  - Test one class: `dotnet test working/content/modulith/ModulithTemplate.slnx --filter-class "*FeatureLayerTests*"`

---

## File Structure

- `working/content/modulith/test/ModulithTemplate.ArchitectureTests/SolutionAssemblies.cs` — **new.** Internal static helper: locate the solution dir, discover+load the `.Features.` layer assemblies, expose them and a lazily-built `Architecture`. Single responsibility: assembly/architecture loading, shared by all architecture tests.
- `working/content/modulith/test/ModulithTemplate.ArchitectureTests/FeatureModuleTests.cs` — **modify.** Remove inline `GetSolutionDirectory` + assembly loading; consume `SolutionAssemblies.Architecture`. Behavior unchanged; this is the regression check for the extraction.
- `working/content/modulith/test/ModulithTemplate.ArchitectureTests/FeatureLayerTests.cs` — **new.** The four layer-rule `[Fact]`s driven by one data table.

No package or csproj changes are required.

---

### Task 1: Extract shared `SolutionAssemblies` loader and refactor `FeatureModuleTests`

**Files:**
- Create: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/SolutionAssemblies.cs`
- Modify: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/FeatureModuleTests.cs`

**Interfaces:**
- Produces:
  - `internal static class SolutionAssemblies`
    - `public static System.Reflection.Assembly[] FeatureAssemblies { get; }` — the discovered feature-layer assemblies (empty array if a `BadImageFormatException` occurs during loading).
    - `public static ArchUnitNET.Domain.Architecture Architecture { get; }` — architecture built from `FeatureAssemblies`.
    - `public static string GetSolutionDirectory()` — walks up from the working dir to the folder containing a `*.slnx`.
- Consumes: nothing from other tasks.

- [ ] **Step 1: Create the shared loader**

Create `working/content/modulith/test/ModulithTemplate.ArchitectureTests/SolutionAssemblies.cs`:

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using Assembly = System.Reflection.Assembly;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Shared discovery and loading of the compiled feature-layer assemblies used by the
/// architecture tests. Assemblies are matched by the <c>.Features.</c> path segment, which
/// corresponds to the per-feature layer projects.
/// </summary>
internal static class SolutionAssemblies
{
    private const string FeaturesKeyword = ".Features.";

    private static readonly Lazy<Assembly[]> LazyFeatureAssemblies = new(LoadFeatureAssemblies);
    private static readonly Lazy<Architecture> LazyArchitecture = new(BuildArchitecture);

    /// <summary>The compiled feature-layer assemblies discovered under the solution directory.</summary>
    public static Assembly[] FeatureAssemblies => LazyFeatureAssemblies.Value;

    /// <summary>The ArchUnitNET architecture built from <see cref="FeatureAssemblies"/>.</summary>
    public static Architecture Architecture => LazyArchitecture.Value;

    /// <summary>Walks up from the current working directory to the folder containing the .slnx.</summary>
    public static string GetSolutionDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }
        return directory!.FullName;
    }

    private static Assembly[] LoadFeatureAssemblies()
    {
        try
        {
            return Directory.GetFiles(GetSolutionDirectory(), "*.dll", SearchOption.AllDirectories)
                .Where(f => f.Contains(FeaturesKeyword, StringComparison.InvariantCulture))
                .DistinctBy(Path.GetFileName)
                .Select(Assembly.LoadFile)
                .ToArray();
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    private static Architecture BuildArchitecture() =>
        new ArchLoader().LoadAssemblies(FeatureAssemblies).Build();
}
```

- [ ] **Step 2: Refactor `FeatureModuleTests` to use the shared loader**

Replace the entire contents of `working/content/modulith/test/ModulithTemplate.ArchitectureTests/FeatureModuleTests.cs` with:

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.xUnitV3;

namespace ModulithTemplate.ArchitectureTests;

public class FeatureModuleTests
{
    [Fact]
    public void ModulesCannotDependOnEachOther()
    {
        const string modulesKeyword = ".Features.";
        const string integrationEventsKeyword = ".IntegrationEvents";

        Architecture architecture = SolutionAssemblies.Architecture;

        var moduleSlice = new SliceAssignment(t =>
        {
            var fullName = t.Namespace.FullName;
            var featureKeywordIdx = fullName.IndexOf(modulesKeyword, StringComparison.Ordinal);

            if (featureKeywordIdx < 0)
            {
                return SliceIdentifier.Ignore();
            }

            var withoutFeaturePrefix = fullName.Substring(featureKeywordIdx + modulesKeyword.Length);

            var nextDotIdx = withoutFeaturePrefix.IndexOf('.');
            var featureName = nextDotIdx < 0 ? withoutFeaturePrefix : withoutFeaturePrefix.Substring(0, nextDotIdx);

            if (withoutFeaturePrefix.StartsWith(featureName + integrationEventsKeyword, StringComparison.Ordinal))
            {
                return SliceIdentifier.Ignore();
            }

            return SliceIdentifier.Of(featureName);
        }, "module slice");

        var ruleCreator = new SliceRuleCreator();
        ruleCreator.SetSliceAssignment(moduleSlice);
        IArchRule noCrossFeatureReference = new GivenSlices(ruleCreator).Should().NotDependOnEachOther();

        noCrossFeatureReference.Check(architecture);
    }
}
```

Note what was removed: `using ArchUnitNET.Loader;`, `using Assembly = System.Reflection.Assembly;`, the `GetSolutionDirectory` method, and the inline assembly-loading/`ArchLoader` block (now in `SolutionAssemblies`).

- [ ] **Step 3: Checkpoint — build warning-clean**

Run: `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror`
Expected: `Build succeeded` with 0 warnings / 0 errors.

- [ ] **Step 4: Checkpoint — existing architecture test still passes**

Run: `dotnet test working/content/modulith/ModulithTemplate.slnx --filter-class "*FeatureModuleTests*"`
Expected: `ModulesCannotDependOnEachOther` passes (1 passed, 0 failed). This proves the loader extraction preserved behavior.

- [ ] **Step 5: Checkpoint — full suite green**

Run: `dotnet test working/content/modulith/ModulithTemplate.slnx`
Expected: all tests pass. (Committing is left to the maintainer per repo policy.)

---

### Task 2: Add `FeatureLayerTests` enforcing the four layer rules

**Files:**
- Create: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/FeatureLayerTests.cs`

**Interfaces:**
- Consumes: `SolutionAssemblies.Architecture` and `SolutionAssemblies.FeatureAssemblies` from Task 1.
- Produces: four `[Fact]` methods (`Domain_depends_on_no_other_layer`, `Application_depends_only_on_Domain`, `Infrastructure_depends_only_on_Domain`, `Web_does_not_access_Domain_directly`).

- [ ] **Step 1: Write the test class with the four facts**

Create `working/content/modulith/test/ModulithTemplate.ArchitectureTests/FeatureLayerTests.cs`:

```csharp
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces the directional dependency rules between the layer projects inside a feature.
/// Layers are identified by assembly name suffix (e.g. <c>ModulithTemplate.Features.Orders.Domain</c>),
/// so the rules apply generically to every current and future feature.
/// </summary>
public class FeatureLayerTests
{
    /// <summary>A feature layer and the sibling layers it must not depend on.</summary>
    private sealed record LayerRule(string Name, string Suffix, string[] Forbidden);

    private static readonly LayerRule[] Rules =
    [
        new("Domain", "Domain", ["Application", "Infrastructure", "Web"]),
        new("Application", "Application", ["Infrastructure", "Web"]),
        new("Infrastructure", "Infrastructure", ["Application", "Web"]),
        new("Web", "Web", ["Domain"]),
    ];

    [Fact]
    public void Domain_depends_on_no_other_layer() => AssertLayerRule("Domain");

    [Fact]
    public void Application_depends_only_on_Domain() => AssertLayerRule("Application");

    [Fact]
    public void Infrastructure_depends_only_on_Domain() => AssertLayerRule("Infrastructure");

    [Fact]
    public void Web_does_not_access_Domain_directly() => AssertLayerRule("Web");

    private static void AssertLayerRule(string layerName)
    {
        var rule = Rules.Single(r => r.Name == layerName);

        // Guard: fail loudly if the layer's assembly was never discovered, so a future naming
        // drift cannot turn the rule into a vacuously-passing no-op.
        var layerPresent = SolutionAssemblies.FeatureAssemblies.Any(a =>
        {
            var name = a.GetName().Name!;
            return name.Contains(".Features.", StringComparison.Ordinal)
                && name.EndsWith("." + rule.Suffix, StringComparison.Ordinal);
        });
        Assert.True(layerPresent,
            $"No assembly for the '{rule.Name}' feature layer was found; the layering rule would pass vacuously.");

        IArchRule layerRule = Types().That().ResideInAssemblyMatching(FeaturePattern(rule.Suffix))
            .Should().NotDependOnAnyTypesThat().ResideInAssemblyMatching(FeaturePattern(Alternation(rule.Forbidden)))
            .Because($"a feature's {rule.Name} layer must not depend on its {string.Join("/", rule.Forbidden)} layer(s).");

        layerRule.Check(SolutionAssemblies.Architecture);
    }

    /// <summary>Builds an assembly-name regex matching a feature layer by its suffix expression.</summary>
    private static string FeaturePattern(string suffixExpression) =>
        @".*\.Features\..*\." + suffixExpression + "$";

    /// <summary>Combines one or more layer suffixes into a regex alternation.</summary>
    private static string Alternation(string[] suffixes) =>
        suffixes.Length == 1 ? suffixes[0] : "(" + string.Join("|", suffixes) + ")";
}
```

- [ ] **Step 2: Run the new facts — expect PASS (feature is currently compliant)**

Run: `dotnet test working/content/modulith/ModulithTemplate.slnx --filter-class "*FeatureLayerTests*"`
Expected: 4 passed, 0 failed. (Unlike unit TDD, an architecture rule over already-compliant code is green immediately; Step 3 proves it actually detects violations.)

- [ ] **Step 3: Prove the test detects a violation (temporary, then revert)**

Temporarily make the Domain layer depend on the Application layer. Add a project reference:

Run:
```bash
dotnet add working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Domain/ModulithTemplate.Features.Orders.Domain.csproj \
  reference working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/ModulithTemplate.Features.Orders.Application.csproj
```

Then edit `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Domain/Entities/SomeEntity.cs` to actually reference an Application type, so a real type dependency exists (a project reference alone is not a type dependency). The Application layer contains the public type `ModulithTemplate.Features.Orders.Application.Class1`.

**Important:** `SomeEntity.cs` has an uncommitted edit by the maintainer — its namespace is `ModulithTemplate.Features.Orders.Domain.Entities` (NOT the old `ModulithTemplate.Features.Example` placeholder). Preserve that namespace. Do **not** use `git checkout` to restore this file (that would revert the maintainer's uncommitted change to the placeholder). Its current contents are:

```csharp
namespace ModulithTemplate.Features.Orders.Domain.Entities;

#pragma warning disable S2094 // Classes should not be empty
public class SomeEntity
#pragma warning restore S2094 // Classes should not be empty
{

}
```

Temporarily replace its contents with (keeping the maintainer's namespace):

```csharp
namespace ModulithTemplate.Features.Orders.Domain.Entities;

public class SomeEntity
{
    // TEMPORARY violation to verify the architecture test detects it.
    private readonly ModulithTemplate.Features.Orders.Application.Class1? _service;
}
```

Run: `dotnet test working/content/modulith/ModulithTemplate.slnx --filter-class "*FeatureLayerTests*"`
Expected: `Domain_depends_on_no_other_layer` **FAILS** with an ArchUnitNET dependency violation naming `SomeEntity` -> `Class1`. (Build with `-warnaserror` would flag the unused field; run the test target directly as shown, which does not pass `-warnaserror`.)

- [ ] **Step 4: Revert the temporary violation**

Run:
```bash
dotnet remove working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Domain/ModulithTemplate.Features.Orders.Domain.csproj \
  reference working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/ModulithTemplate.Features.Orders.Application.csproj
```

Restore `SomeEntity.cs` by hand-editing it back to the maintainer's current version (preserve the `ModulithTemplate.Features.Orders.Domain.Entities` namespace — do NOT `git checkout` the file):

```csharp
namespace ModulithTemplate.Features.Orders.Domain.Entities;

#pragma warning disable S2094 // Classes should not be empty
public class SomeEntity
#pragma warning restore S2094 // Classes should not be empty
{

}
```

Verify only the intended files differ from the maintainer's pre-task working tree:
Run: `git -C /workspaces/modulith-template status --short`
Expected: `SomeEntity.cs` shows its pre-existing namespace edit only (no temp field, no added project reference); the new test files are untracked/added.

- [ ] **Step 5: Checkpoint — build warning-clean and full suite green**

Run: `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror`
Expected: 0 warnings, 0 errors.

Run: `dotnet test working/content/modulith/ModulithTemplate.slnx`
Expected: all tests pass, including the four new `FeatureLayerTests` facts. (Committing is left to the maintainer per repo policy.)

---

## Notes for the implementer

- **Why assembly, not namespace:** `src/Features/Orders/.../Domain/Entities/SomeEntity.cs` lives in `namespace ModulithTemplate.Features.Example` — no `.Domain` suffix. Namespace-based matching would skip the Domain layer entirely and the rule would pass without checking anything. Assembly names always carry the layer suffix.
- **`ResideInAssemblyMatching` takes a regex string**; that regex is evaluated inside ArchUnitNET, so it does not trip the Meziantou regex-timeout analyzer. Keep all regex out of your own code (the presence guard deliberately uses `Contains`/`EndsWith`).
- **Genericity:** the `.*\.Features\..*` portion matches any feature. A new feature folder is covered with no test change. A brand-new *layer* would be one new `LayerRule` row plus one new `[Fact]`.
- **Scope:** only `.Features.*` assemblies are constrained; root `ModulithTemplate.Infrastructure`/`.Web`/`.Web.Common`/`.FeatureCore` are excluded by the required `.Features.` segment.
```

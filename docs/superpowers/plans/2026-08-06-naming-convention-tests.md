# Naming Convention Architecture Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `NamingConventionTests` class to the template's architecture test project that enforces, in both directions, the naming and placement of the four DDD building blocks — specifications, mappers, app services, and domain services.

**Architecture:** A single new file in `working/content/modulith/test/ModulithTemplate.ArchitectureTests/`. A four-row convention table drives eight thin `[Fact]`s through two shared helpers (`AssertNaming`, `AssertPlacement`). Namespaces are matched by regex against a per-feature pattern (`.*\.Features\..*\.Domain\.Specifications(\..*)?$`), never by literal namespace, so every present and future feature is covered and no `ModulithTemplate` token leaks into generated projects. The rules consume the existing `SolutionAssemblies.Architecture`; no csproj or package changes.

**Tech Stack:** .NET 10, xUnit v3 on Microsoft.Testing.Platform, ArchUnitNET (`TngTech.ArchUnitNET.xUnitV3` 0.13.3, already referenced).

**Spec:** `docs/superpowers/specs/2026-08-06-naming-convention-tests-design.md`

## Global Constraints

- **Do not perform any git actions.** Both `CLAUDE.md` files state this explicitly ("Do not perform any git actions unless explicitly asked — the maintainer handles commits, branches, and releases"). This overrides the writing-plans skill's default "commit after each task" step. Every task therefore ends at a verification command, not a commit. Leave changes in the working tree for the maintainer.
- **This repo is a `dotnet new` template package, not an application.** All work happens under `working/content/`. Paths below are relative to the repo root `/workspaces/modulith-template`.
- **`sourceName` is `ModulithTemplate`.** Every namespace and identifier introduced under `working/content/modulith/` must keep the `ModulithTemplate` prefix where it names the app. Conversely, **no literal namespace may appear inside the arch rules** — they must be regex patterns keyed on `.Features.`, or they would match only one feature and would hard-code the template's name.
- **Tests must run from the content directory.** `global.json` opts into Microsoft.Testing.Platform and is resolved from the current directory. Always `cd working/content/modulith` first. Passing the `.slnx` as a path argument from the repo root gets forwarded to the test app and silently runs zero tests.
- **No new packages and no csproj edits.** `TngTech.ArchUnitNET.xUnitV3` 0.13.3 is already referenced by `ModulithTemplate.ArchitectureTests.csproj`, and its `<ProjectReference Include="../../src/Features/**/*.csproj" />` glob already forces every feature assembly to build alongside the tests.
- **Using-directive layout is enforced under `-warnaserror`** by `.editorconfig`: `dotnet_sort_system_directives_first = true` and `dotnet_separate_import_directive_groups = true`. Each root namespace group is separated by a blank line, `System.*` first. Violations surface as IDE0055. The exact block for the new file is given in Task 1.
- **MA0048 (Meziantou) requires the file name to match the type name.** `NamingConventionTests.cs` must contain `public class NamingConventionTests`.
- **XML doc comments are required on public classes and methods** per the generated project's `CLAUDE.md`.
- **Do not add "assembly was discovered" presence guards** to these rules, unlike `FeatureLayerTests` and `FeatureModuleTests`. Per the spec's "Key design decision: accept vacuity", all eight rules legitimately match zero types in a scaffolded solution. The class-level `<remarks>` in Task 1 records this so a future reader does not "fix" it.
- **Analyzer warnings on temporary fixture files are expected and harmless.** Fixture cycles run with plain `dotnet test`, which does not pass `-warnaserror`. The `-warnaserror` build happens in Task 5, after every fixture has been deleted.

## API facts verified against `ArchUnitNET.dll` 0.13.3

These were confirmed by compiling a throwaway probe against the real package, not inferred:

- `Types().That().ResideInNamespaceMatching(pattern).And().AreNotNested().Should().HaveNameEndingWith(suffix).Because(reason)` compiles.
- `Types().That().HaveNameEndingWith(suffix).And().AreNotNested().Should().ResideInNamespaceMatching(pattern).Because(reason)` compiles.
- Both expressions are assignable to `IArchRule` and expose `.Check(Architecture)`.

## File Structure

| File | Responsibility |
|------|----------------|
| `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs` | **Create.** The convention table, the two rule helpers, the namespace-pattern builder, and eight `[Fact]`s. |
| `working/content/modulith/CLAUDE.md` | **Modify** (lines 84, 86, 87). Align the documented folder conventions with what the tests now enforce. |
| Temporary fixture files under `src/Features/Orders/` | **Create then delete** within each of Tasks 1–4. They exist only to drive a red/green cycle and must not survive the task that created them. |

---

### Task 1: `NamingConventionTests` skeleton and the Specification convention

Creates the file with its table, both helpers, the pattern builder, and the first convention's two facts. Because a fresh scaffold contains no specifications, the rules would pass vacuously — so the test-first cycle here drives them with a temporary *violating* type, proving the regex actually matches before the rule is allowed to go green.

**Files:**
- Create: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs`
- Create then delete: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Domain/Specifications/SomeEntityById.cs`

**Interfaces:**
- Consumes: `SolutionAssemblies.Architecture` (existing, `internal static`, type `ArchUnitNET.Domain.Architecture`) from `SolutionAssemblies.cs`.
- Produces: `private sealed record NamingConvention(string Name, string NamespaceSuffix, string TypeSuffix)`; `private static readonly NamingConvention[] Conventions`; `private static void AssertNaming(string conventionName)`; `private static void AssertPlacement(string conventionName)`; `private static string FeatureNamespacePattern(string namespaceSuffix)`. Tasks 2–4 add rows to `Conventions` and call the two helpers by convention name — the names are the lowercase `Name` values in the table.

- [ ] **Step 1: Create the temporary violating fixture**

This type sits in the Specifications namespace but does **not** end in `Spec`, so it must break the naming rule. Create `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Domain/Specifications/SomeEntityById.cs`:

```csharp
namespace ModulithTemplate.Features.Orders.Domain.Specifications;

/// <summary>Temporary fixture proving the naming rule matches. Deleted in Step 9.</summary>
public sealed class SomeEntityById
{
    /// <summary>The identifier being matched.</summary>
    public Guid Id { get; init; }
}
```

- [ ] **Step 2: Write the failing test**

Create `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs`. Note the using block: `System.*` first, then a blank line, then the `ArchUnitNET` group, then a blank line before `using static` — required by `dotnet_separate_import_directive_groups`.

```csharp
using System.Text.RegularExpressions;

using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Enforces the naming and placement conventions for the feature building blocks described in
/// CLAUDE.md: specifications, mappers, app services and domain services. Each convention is
/// checked in both directions — every type in the home namespace carries the suffix, and every
/// type carrying the suffix resides in the home namespace.
/// </summary>
/// <remarks>
/// Unlike <see cref="FeatureLayerTests"/> and <see cref="FeatureModuleTests"/>, these rules
/// deliberately carry no "assembly was discovered" presence guard, and none should be added.
/// A layering rule that matches nothing is a false green over code that exists; a naming rule
/// that matches nothing simply means the solution has no specifications yet, which is the
/// normal state of a freshly scaffolded project. These are conditional tripwires armed for
/// code the user has not written yet.
/// </remarks>
public class NamingConventionTests
{
    /// <summary>A building block, the namespace it belongs in, and the type-name suffix it carries.</summary>
    private sealed record NamingConvention(string Name, string NamespaceSuffix, string TypeSuffix);

    private static readonly NamingConvention[] Conventions =
    [
        new("specification", "Domain.Specifications", "Spec"),
    ];

    /// <summary>Every type in a feature's Specifications namespace must be named <c>*Spec</c>.</summary>
    [Fact]
    public void Types_in_Specifications_end_with_Spec() => AssertNaming("specification");

    /// <summary>Every type named <c>*Spec</c> must reside in a feature's Specifications namespace.</summary>
    [Fact]
    public void Types_named_Spec_reside_in_Specifications() => AssertPlacement("specification");

    private static void AssertNaming(string conventionName)
    {
        var convention = Find(conventionName);

        IArchRule rule = Types().That()
            .ResideInNamespaceMatching(FeatureNamespacePattern(convention.NamespaceSuffix))
            .And().AreNotNested()
            .Should().HaveNameEndingWith(convention.TypeSuffix)
            .Because($"every type in a feature's {convention.NamespaceSuffix} namespace is a {convention.Name} and must be named *{convention.TypeSuffix}.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    private static void AssertPlacement(string conventionName)
    {
        var convention = Find(conventionName);

        IArchRule rule = Types().That()
            .HaveNameEndingWith(convention.TypeSuffix)
            .And().AreNotNested()
            .Should().ResideInNamespaceMatching(FeatureNamespacePattern(convention.NamespaceSuffix))
            .Because($"a {convention.Name} must live in its feature's {convention.NamespaceSuffix} namespace.");

        rule.Check(SolutionAssemblies.Architecture);
    }

    private static NamingConvention Find(string conventionName) =>
        Conventions.Single(c => string.Equals(c.Name, conventionName, StringComparison.Ordinal));

    /// <summary>
    /// Builds a namespace regex matching one building block's home namespace in any feature,
    /// e.g. <c>Domain.Specifications</c> becomes
    /// <c>.*\.Features\..*\.Domain\.Specifications(\..*)?$</c>. The trailing group admits
    /// sub-namespaces while still rejecting a sibling whose name merely starts with the same
    /// text, because after the suffix the pattern requires either end-of-string or a dot.
    /// </summary>
    private static string FeatureNamespacePattern(string namespaceSuffix) =>
        @".*\.Features\..*\." + Regex.Escape(namespaceSuffix) + @"(\..*)?$";
}
```

- [ ] **Step 3: Run the naming test to verify it fails**

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Specifications_end_with_Spec*"
```

Expected: **FAIL**, naming `SomeEntityById` as the offending type. This is the load-bearing signal — it proves `FeatureNamespacePattern("Domain.Specifications")` actually matches a real namespace. A pass here would mean the regex matches nothing and the rule is inert.

- [ ] **Step 4: Rename the fixture to conform**

Rename the file to `SomeEntityByIdSpec.cs` and the type to match (MA0048 requires they agree):

```csharp
namespace ModulithTemplate.Features.Orders.Domain.Specifications;

/// <summary>Temporary fixture proving the naming rule accepts a conforming type. Deleted in Step 9.</summary>
public sealed class SomeEntityByIdSpec
{
    /// <summary>The identifier being matched.</summary>
    public Guid Id { get; init; }
}
```

- [ ] **Step 5: Run the naming test to verify it passes**

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Specifications_end_with_Spec*"
```

Expected: **PASS**.

- [ ] **Step 6: Verify `.AreNotNested()` is actually load-bearing**

The spec calls this filter load-bearing: lambda closures and async state machines are nested compiler-generated types that report the *enclosing* namespace, so without the filter the naming rule would fail the first time a user writes a LINQ expression inside a `*Spec`. Force such a type into existence by adding a method that captures a local, then confirm the rule tolerates it. Edit `SomeEntityByIdSpec.cs`:

```csharp
namespace ModulithTemplate.Features.Orders.Domain.Specifications;

/// <summary>Temporary fixture proving the naming rule accepts a conforming type. Deleted in Step 9.</summary>
public sealed class SomeEntityByIdSpec
{
    /// <summary>The identifier being matched.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Filters identifiers against a captured local, forcing the compiler to emit a nested
    /// display class inside this type's namespace.
    /// </summary>
    /// <param name="ids">The identifiers to filter.</param>
    /// <param name="target">The identifier to match against.</param>
    /// <returns>The matching identifiers.</returns>
    public static IEnumerable<Guid> Matching(IEnumerable<Guid> ids, Guid target) =>
        ids.Where(i => i == target);
}
```

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Specifications_end_with_Spec*"
```

Expected: **PASS**.

Now probe whether the filter is what made it pass. Temporarily delete `.And().AreNotNested()` from `AssertNaming` and re-run the same command, then interpret:

- **If it now FAILS** naming a `<>c__DisplayClass…` type — `.AreNotNested()` is confirmed load-bearing. Restore it and re-run to get back to green.
- **If it still PASSES** — ArchUnitNET is already filtering compiler-generated types itself. Restore `.And().AreNotNested()` anyway as cheap insurance against nested *hand-written* helper types, and note this finding in the Task 6 handoff so the spec's rationale can be corrected.

Either way, `.And().AreNotNested()` must be present in both helpers before moving on.

- [ ] **Step 7: Break placement to verify the second direction fails**

Move the conforming type out of its home namespace by editing only the namespace line of `SomeEntityByIdSpec.cs` (leave the file where it is — the rule keys on namespace, not path):

```csharp
namespace ModulithTemplate.Features.Orders.Domain;
```

Then run:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_named_Spec_reside_in_Specifications*"
```

Expected: **FAIL**, naming `SomeEntityByIdSpec` as residing outside `Domain.Specifications`.

- [ ] **Step 8: Restore the namespace and verify placement passes**

Change the namespace line back to `ModulithTemplate.Features.Orders.Domain.Specifications;`, then:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-class "*NamingConventionTests*"
```

Expected: **PASS**, 2 tests.

- [ ] **Step 9: Delete the fixture and confirm the rules go vacuously green**

```bash
cd /workspaces/modulith-template/working/content/modulith
rm -r src/Features/Orders/ModulithTemplate.Features.Orders.Domain/Specifications
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-class "*NamingConventionTests*"
```

Expected: **PASS**, 2 tests. Confirm with `git status` that no file under `src/Features/Orders/` remains modified or untracked — the only change from this task is the new test file.

---

### Task 2: Mapper convention

**Files:**
- Modify: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs`
- Create then delete: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/Mappers/SomeEntityMapping.cs`

**Interfaces:**
- Consumes: `Conventions`, `AssertNaming`, `AssertPlacement` from Task 1.
- Produces: a `"mapper"` row in `Conventions`.

- [ ] **Step 1: Create the temporary violating fixture**

Named `SomeEntityMapping`, not `*Mapper`, so it must break the naming rule. Create `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/Mappers/SomeEntityMapping.cs`:

```csharp
namespace ModulithTemplate.Features.Orders.Application.Mappers;

/// <summary>Temporary fixture proving the naming rule matches. Deleted in Step 6.</summary>
public sealed class SomeEntityMapping
{
    /// <summary>Returns the supplied value unchanged.</summary>
    /// <param name="value">The value to pass through.</param>
    /// <returns>The unchanged value.</returns>
    public static string Map(string value) => value;
}
```

- [ ] **Step 2: Write the failing test**

In `NamingConventionTests.cs`, add the table row and the two facts. The `Conventions` array becomes:

```csharp
    private static readonly NamingConvention[] Conventions =
    [
        new("specification", "Domain.Specifications", "Spec"),
        new("mapper", "Application.Mappers", "Mapper"),
    ];
```

And add these two facts directly below the two Specification facts:

```csharp
    /// <summary>Every type in a feature's Mappers namespace must be named <c>*Mapper</c>.</summary>
    [Fact]
    public void Types_in_Mappers_end_with_Mapper() => AssertNaming("mapper");

    /// <summary>Every type named <c>*Mapper</c> must reside in a feature's Mappers namespace.</summary>
    [Fact]
    public void Types_named_Mapper_reside_in_Mappers() => AssertPlacement("mapper");
```

- [ ] **Step 3: Run the naming test to verify it fails**

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Mappers_end_with_Mapper*"
```

Expected: **FAIL**, naming `SomeEntityMapping`.

- [ ] **Step 4: Rename the fixture to conform and verify it passes**

Rename the file to `SomeEntityMapper.cs` and the type to `SomeEntityMapper` (MA0048), then:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Mappers_end_with_Mapper*"
```

Expected: **PASS**.

- [ ] **Step 5: Verify the placement direction both ways**

Edit only the namespace line of `SomeEntityMapper.cs` to `ModulithTemplate.Features.Orders.Application;` and run:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_named_Mapper_reside_in_Mappers*"
```

Expected: **FAIL**, naming `SomeEntityMapper`. Then change the namespace line back to `ModulithTemplate.Features.Orders.Application.Mappers;` and re-run the same command.

Expected: **PASS**.

- [ ] **Step 6: Delete the fixture and confirm green**

```bash
cd /workspaces/modulith-template/working/content/modulith
rm -r src/Features/Orders/ModulithTemplate.Features.Orders.Application/Mappers
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-class "*NamingConventionTests*"
```

Expected: **PASS**, 4 tests. Confirm with `git status` that nothing under `src/Features/Orders/` remains changed.

---

### Task 3: App service convention

**Files:**
- Modify: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs`
- Create then delete: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/Services/IOrdersOperations.cs`

**Interfaces:**
- Consumes: `Conventions`, `AssertNaming`, `AssertPlacement` from Task 1.
- Produces: an `"app service"` row in `Conventions`.

The fixture is an interface rather than a class, exercising the spec's decision to use `Types()` rather than `Classes()` so that `I*AppService` abstractions are covered.

- [ ] **Step 1: Create the temporary violating fixture**

Create `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/Services/IOrdersOperations.cs`:

```csharp
namespace ModulithTemplate.Features.Orders.Application.Services;

/// <summary>Temporary fixture proving the naming rule matches interfaces. Deleted in Step 6.</summary>
public interface IOrdersOperations
{
    /// <summary>Does nothing; exists so the fixture is not an empty type.</summary>
    void Noop();
}
```

- [ ] **Step 2: Write the failing test**

Add the table row so `Conventions` becomes:

```csharp
    private static readonly NamingConvention[] Conventions =
    [
        new("specification", "Domain.Specifications", "Spec"),
        new("mapper", "Application.Mappers", "Mapper"),
        new("app service", "Application.Services", "AppService"),
    ];
```

And add these two facts below the Mapper facts:

```csharp
    /// <summary>Every type in a feature's Application Services namespace must be named <c>*AppService</c>.</summary>
    [Fact]
    public void Types_in_Application_Services_end_with_AppService() => AssertNaming("app service");

    /// <summary>Every type named <c>*AppService</c> must reside in a feature's Application Services namespace.</summary>
    [Fact]
    public void Types_named_AppService_reside_in_Application_Services() => AssertPlacement("app service");
```

- [ ] **Step 3: Run the naming test to verify it fails**

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Application_Services_end_with_AppService*"
```

Expected: **FAIL**, naming `IOrdersOperations`.

- [ ] **Step 4: Rename the fixture to conform and verify it passes**

Rename the file to `IOrdersAppService.cs` and the type to `IOrdersAppService`, then:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Application_Services_end_with_AppService*"
```

Expected: **PASS**. (`IOrdersAppService` ends with `AppService`; the leading `I` is irrelevant to a suffix rule.)

- [ ] **Step 5: Verify the placement direction both ways**

Edit only the namespace line of `IOrdersAppService.cs` to `ModulithTemplate.Features.Orders.Application;` and run:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_named_AppService_reside_in_Application_Services*"
```

Expected: **FAIL**, naming `IOrdersAppService`. Then change the namespace line back to `ModulithTemplate.Features.Orders.Application.Services;` and re-run.

Expected: **PASS**.

- [ ] **Step 6: Delete the fixture and confirm green**

```bash
cd /workspaces/modulith-template/working/content/modulith
rm -r src/Features/Orders/ModulithTemplate.Features.Orders.Application/Services
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-class "*NamingConventionTests*"
```

Expected: **PASS**, 6 tests. Confirm with `git status` that nothing under `src/Features/Orders/` remains changed.

---

### Task 4: Domain service convention

**Files:**
- Modify: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs`
- Create then delete: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Domain/Services/OrdersPricingRules.cs`

**Interfaces:**
- Consumes: `Conventions`, `AssertNaming`, `AssertPlacement` from Task 1.
- Produces: a `"domain service"` row in `Conventions`. After this task the table is complete at four rows and the class has all eight facts.

- [ ] **Step 1: Create the temporary violating fixture**

Create `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Domain/Services/OrdersPricingRules.cs`:

```csharp
namespace ModulithTemplate.Features.Orders.Domain.Services;

/// <summary>Temporary fixture proving the naming rule matches. Deleted in Step 6.</summary>
public sealed class OrdersPricingRules
{
    /// <summary>Does nothing; exists so the fixture is not an empty type.</summary>
    public static void Noop()
    {
    }
}
```

- [ ] **Step 2: Write the failing test**

Add the final table row so `Conventions` becomes:

```csharp
    private static readonly NamingConvention[] Conventions =
    [
        new("specification", "Domain.Specifications", "Spec"),
        new("mapper", "Application.Mappers", "Mapper"),
        new("app service", "Application.Services", "AppService"),
        new("domain service", "Domain.Services", "DomainService"),
    ];
```

And add these two facts below the app service facts:

```csharp
    /// <summary>Every type in a feature's Domain Services namespace must be named <c>*DomainService</c>.</summary>
    [Fact]
    public void Types_in_Domain_Services_end_with_DomainService() => AssertNaming("domain service");

    /// <summary>Every type named <c>*DomainService</c> must reside in a feature's Domain Services namespace.</summary>
    [Fact]
    public void Types_named_DomainService_reside_in_Domain_Services() => AssertPlacement("domain service");
```

- [ ] **Step 3: Run the naming test to verify it fails**

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Domain_Services_end_with_DomainService*"
```

Expected: **FAIL**, naming `OrdersPricingRules`.

- [ ] **Step 4: Rename the fixture to conform and verify it passes**

Rename the file to `OrdersPricingDomainService.cs` and the type to `OrdersPricingDomainService`, then:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-method "*Types_in_Domain_Services_end_with_DomainService*"
```

Expected: **PASS**.

- [ ] **Step 5: Verify the placement direction, and confirm the two Services conventions do not collide**

Edit only the namespace line of `OrdersPricingDomainService.cs` to `ModulithTemplate.Features.Orders.Domain;` and run the whole class:

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-class "*NamingConventionTests*"
```

Expected: exactly **one** failure — `Types_named_DomainService_reside_in_Domain_Services`. In particular `Types_named_AppService_reside_in_Application_Services` must still pass: `OrdersPricingDomainService` does not end with `AppService`, so the two `Services` conventions are independent. If that test also fails, the suffixes are colliding and the table is wrong.

Then change the namespace line back to `ModulithTemplate.Features.Orders.Domain.Services;` and re-run the same command.

Expected: **PASS**, 8 tests.

- [ ] **Step 6: Delete the fixture and confirm green**

```bash
cd /workspaces/modulith-template/working/content/modulith
rm -r src/Features/Orders/ModulithTemplate.Features.Orders.Domain/Services
dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-class "*NamingConventionTests*"
```

Expected: **PASS**, 8 tests. Confirm with `git status` that the only changed file in the whole repo is the new `NamingConventionTests.cs`.

---

### Task 5: Full-solution and scaffold verification

Everything so far ran a single test project without `-warnaserror`. This task proves the new file is warning-clean, does not disturb the existing architecture tests, and survives the template rename.

**Files:**
- No source changes expected. If `-warnaserror` surfaces a diagnostic, fix it in `NamingConventionTests.cs` and re-run from Step 1.

- [ ] **Step 1: Build the content solution exactly as CI does**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
```

Expected: **Build succeeded, 0 warnings.** IDE0055 here means the using groups are not blank-line separated with `System.*` first; MA0048 means the file name and type name disagree.

- [ ] **Step 2: Run the whole test suite**

```bash
cd /workspaces/modulith-template/working/content/modulith
dotnet test
```

Expected: **PASS**. The eight new facts pass, and the pre-existing `FeatureLayerTests` (4 facts) and `FeatureModuleTests` (1 fact) are unaffected — they share `SolutionAssemblies` but this task added nothing to it.

- [ ] **Step 3: Scaffold a project and confirm the template token did not leak**

```bash
cd /workspaces/modulith-template
dotnet new install working/content/modulith
dotnet new modulith -n ScaffoldCheck -o /tmp/ScaffoldCheck
grep -rn "ModulithTemplate" /tmp/ScaffoldCheck/test/ScaffoldCheck.ArchitectureTests/NamingConventionTests.cs
```

Expected: **no matches** (grep exits 1). The file's only app-name-bearing token is its `namespace ScaffoldCheck.ArchitectureTests;` line, which the engine rewrote; the arch rules themselves are pure regex keyed on `.Features.` and contain no app name.

- [ ] **Step 4: Confirm the scaffolded project's tests pass, then clean up**

```bash
cd /tmp/ScaffoldCheck && dotnet test
cd /workspaces/modulith-template
dotnet new uninstall working/content/modulith
rm -rf /tmp/ScaffoldCheck
```

Expected: **PASS**, including the eight naming facts passing vacuously in a project that has no specifications, mappers, app services, or domain services. This is the end-user's day-one experience and must be green.

---

### Task 6: Align `working/content/modulith/CLAUDE.md` with the enforced conventions

The generated project's `CLAUDE.md` currently documents `Mapper/` (singular) and gives domain services and app services no folder at all. Left alone, the guidance shipped to end users would contradict the tests shipped beside it — a user following the docs would write `Mapper/OrdersMapper.cs` and fail `Types_named_Mapper_reside_in_Mappers`.

**Files:**
- Modify: `working/content/modulith/CLAUDE.md` (lines 84, 86, 87)

- [ ] **Step 1: Update the Domain bullet (line 86) to give domain services a folder**

Find:

```
Cross-entity rules that need data access live in domain services (`*DomainService`).
```

Replace with:

```
Cross-entity rules that need data access live in domain services, as `*DomainService` classes under `Services/`.
```

- [ ] **Step 2: Update the Application bullet (line 87) for `Mappers/` and `Services/`**

Find:

```
App services (`I*AppService`, `internal` impls) load entities via repositories, invoke domain services, persist, and map to DTOs.
```

Replace with:

```
App services live under `Services/` (`I*AppService`, `internal` impls); they load entities via repositories, invoke domain services, persist, and map to DTOs.
```

Then find, in the same bullet:

```
Entity↔DTO mapping uses **Mapperly** source generators (`Mapper/*Mapper.cs`, `[Mapper]` partial classes).
```

Replace with:

```
Entity↔DTO mapping uses **Mapperly** source generators (`Mappers/*Mapper.cs`, `[Mapper]` partial classes).
```

- [ ] **Step 3: Note the new enforcement in the architecture paragraph (line 84)**

Find:

```
Feature modules must not depend on each other (enforced by `ModulithTemplate.ArchitectureTests`).
```

Replace with:

```
Feature modules must not depend on each other, and the building-block naming and placement conventions below (`Specifications/`, `Services/`, `Mappers/`) are enforced in both directions — all by `ModulithTemplate.ArchitectureTests`.
```

- [ ] **Step 4: Verify the docs and the table now agree**

```bash
cd /workspaces/modulith-template
grep -n "Specifications/\|Mappers/\|Services/\|Mapper/" working/content/modulith/CLAUDE.md
```

Expected: matches for `Specifications/`, `Mappers/` and `Services/`; **no** remaining match for the singular `Mapper/` as a folder. Cross-check each against the `Conventions` table in `NamingConventionTests.cs` — four folder names, four table rows, same spellings.

- [ ] **Step 5: Re-run the full build and test suite**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && dotnet test
```

Expected: **Build succeeded, 0 warnings**; all tests **PASS**. (A markdown-only change cannot break the build; this is a cheap confirmation that the tree is being handed over green.)

- [ ] **Step 6: Hand off to the maintainer**

Do not commit. Run `git status` and report the changed files — `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs` (new) and `working/content/modulith/CLAUDE.md` (modified) — and confirm no temporary fixture files survive anywhere under `src/Features/Orders/`.

---

## Out of scope

Per the spec, none of the following are part of this plan:

- Seeding the `Orders` sample feature with permanent example specifications, mappers, app services or domain services.
- Any change to the `modulith-feature` sub-template (`working/content/feature/`). Empty directories do not survive scaffolding and there is nothing to place in them.
- Rules for conventions the scaffold already satisfies implicitly (`*Context`, `*Repository`, `I<Feature>Repository`, `*Module`).
- Rules beyond naming and placement — visibility (`internal` implementations), attribute presence (`[Mapper]`), or base-type requirements (`Specification<T>`).
- Bumping `<PackageVersion>` in `working/ModularMonolith.Template.csproj`. Releasing is the maintainer's call.

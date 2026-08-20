# Behaviours to SharedKernel.Application — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the three mediator pipeline behaviours and `ValidationError` referenceable by any host by moving them from `ModulithTemplate.Web` / `SharedKernel.Web` into `SharedKernel.Application`.

**Architecture:** Pure relocation plus a visibility widening — no behaviour changes. `LoggingBehaviour`'s dependency on `ServiceDefaults` is severed first (Task 3) by inlining the ActivitySource name and switching OpenTelemetry to a prefix subscription, so the behaviour move that follows is dependency-free. Hosts keep configuring `options.PipelineBehaviors` themselves; nothing becomes automatic.

**Tech Stack:** .NET 10, martinothamar `Mediator` 3.0.2 (source-generated), FluentResults 4.0.0, FluentValidation 12.1.1, OpenTelemetry 1.17.0, xUnit v3 on Microsoft.Testing.Platform, NSubstitute, central package management.

**Spec:** `docs/superpowers/specs/2026-08-20-behaviours-to-sharedkernel-application-design.md`

## Global Constraints

- **No git actions.** The repo's `CLAUDE.md` reserves commits, branches and releases for the maintainer. Tasks end at a verification gate, never a commit. Do not run `git add`, `git commit`, `git checkout` or `git branch`.
- **All work is in `working/content/modulith/`** (the solution template) and `working/content/feature/` (the feature sub-template). Nothing at the repo root changes.
- **`sourceName` is `ModulithTemplate`** in the solution template and `FeatureName` + `ModulithApp` in the feature template. Every new identifier, namespace, path and string literal must carry the right token, or it leaks the template's name into generated projects.
- **Build must be warning-clean:** `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror`. Meziantou, Sonar and Roslynator analyzers are on with `EnforceCodeStyleInBuild`.
- **Tests must be prefixed with `rtk proxy` and run from `working/content/modulith/`.** Without the prefix the RTK hook rewrites to `rtk dotnet test`, injecting `--report-trx`, which no test app here implements — the run reports "Zero tests ran" and *passes*. Treat a zero-test run as a failure.
- **Central package management:** add versions to `working/content/modulith/Directory.Packages.props`; `PackageReference` in a csproj is version-less. Every package this plan needs already has a `PackageVersion` entry — no new versions required.
- **Comment style:** an XML `<summary>` is a line or two; a `<remarks>` earns its space only by recording what the code cannot say. Cut rather than polish.
- **`using` grouping:** this codebase separates `using` groups by a blank line between top-level namespace roots (`System`, third-party, `ModulithTemplate.*`). Match the file you are editing.

---

### Task 1: Characterization test for pipeline registration

The riskiest assumption in this whole change is unverified: that `AddMediator` still registers pipeline behaviours when their types live in a *referenced* assembly rather than the one holding `Mediator.SourceGenerator`. No existing test covers it — the three behaviour tests instantiate the behaviours directly and never touch DI. Write that test now, against the current code, so it passes before the move and guards it afterwards.

**Files:**
- Create: `working/content/modulith/test/ModulithTemplate.WebTests/MediatorPipelineRegistrationTests.cs`

**Interfaces:**
- Consumes: `AddMediator` (generated into the `ModulithTemplate.Web` assembly by `Mediator.SourceGenerator`, surfaced as a `Microsoft.Extensions.DependencyInjection` extension), and the three behaviour types.
- Produces: a test that later tasks re-run unchanged except for its `using` of the behaviours' namespace.

- [ ] **Step 1: Probe how Mediator actually registers behaviours**

The exact `ServiceDescriptor` shape Mediator 3.0.2 emits for `options.PipelineBehaviors` is not documented and must be observed, not guessed. Write this throwaway probe first:

```csharp
using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Web.Behaviours;

namespace ModulithTemplate.WebTests;

public class MediatorPipelineRegistrationTests
{
    [Fact]
    public void Probe()
    {
        var services = new ServiceCollection();
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehaviour<,>),
                typeof(ExceptionBehaviour<,>),
                typeof(ValidationBehaviour<,>),
            ];
        });

        foreach (var descriptor in services.Where(d =>
            d.ServiceType.Name.Contains("PipelineBehavior", StringComparison.Ordinal)))
        {
            Assert.Fail($"{descriptor.ServiceType} -> {descriptor.ImplementationType} ({descriptor.Lifetime})");
        }

        Assert.Fail("no IPipelineBehavior descriptors found at all");
    }
}
```

- [ ] **Step 2: Run the probe and read the output**

Run: `cd working/content/modulith && rtk proxy dotnet test --project test/ModulithTemplate.WebTests --filter-class "*MediatorPipelineRegistrationTests*"`

Expected: FAIL, with a message naming the first descriptor. `Assert.Fail` is used deliberately — it is the cheapest way to surface the shape.

Record what you see. Two outcomes are plausible:
- **(a)** open-generic descriptors: `IPipelineBehavior\`2 -> LoggingBehaviour\`2`, one per behaviour.
- **(b)** no descriptors, meaning the generator bakes the pipeline into generated per-message code instead of DI.

- [ ] **Step 3: Write the real test based on what you observed**

If **(a)**, replace the probe body's assertion block with:

```csharp
        var registered = services
            .Where(descriptor => descriptor.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(descriptor => descriptor.ImplementationType)
            .ToArray();

        Assert.Equal(
            [typeof(LoggingBehaviour<,>), typeof(ExceptionBehaviour<,>), typeof(ValidationBehaviour<,>)],
            registered);
```

and rename the test to `AddMediator_registers_the_solution_pipeline_behaviours_in_order`. Add a one-line `<summary>`: "Guards that behaviours stay reachable from `AddMediator` once they live in another assembly."

If **(b)**, DI descriptors cannot carry the guarantee. Instead assert the generated mediator resolves and the pipeline runs, by registering a test command and handler and asserting `ExceptionBehaviour` converted a thrown exception into a failed `Result` — the same `TestCommand`/`TestHandler` shape already used in `ValidationBehaviourTests.cs`. **Stop and report which branch you took before continuing** — branch (b) means the behaviour types must be visible to the generator's compilation, which would change Task 4's approach.

- [ ] **Step 4: Run the test and verify it passes**

Run: `cd working/content/modulith && rtk proxy dotnet test --project test/ModulithTemplate.WebTests --filter-class "*MediatorPipelineRegistrationTests*"`

Expected: PASS, and the run reports 1 test — not zero.

- [ ] **Step 5: Verify the whole suite is green before any move**

Run: `cd working/content/modulith && rtk proxy dotnet test`

Expected: all tests pass. This is the baseline every later task compares against.

---

### Task 2: Move `ValidationError` to `SharedKernel.Application`

Self-contained and independently justified: today `SharedKernel.Web` owns it, and feature `Application` projects do not reference `SharedKernel.Web`, so a handler cannot produce a `ValidationError` even when a failure is semantically a field error.

**Files:**
- Create: `working/content/modulith/src/SharedKernel/ModulithTemplate.SharedKernel.Application/Errors/ValidationError.cs`
- Delete: `working/content/modulith/src/SharedKernel/ModulithTemplate.SharedKernel.Web/Errors/ValidationError.cs` (and the now-empty `Errors/` folder)
- Modify: `working/content/modulith/src/SharedKernel/ModulithTemplate.SharedKernel.Application/ModulithTemplate.SharedKernel.Application.csproj`
- Modify: `working/content/modulith/src/SharedKernel/ModulithTemplate.SharedKernel.Web/ModulithTemplate.SharedKernel.Web.csproj`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/Behaviours/ValidationBehaviour.cs:8`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/Components/_Imports.razor:10`
- Modify: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Web/Components/_Imports.razor:7`
- Modify: `working/content/modulith/test/ModulithTemplate.WebTests/ValidationBehaviourTests.cs:8`
- Modify: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/Components/_Imports.razor:7`

**Interfaces:**
- Produces: `ModulithTemplate.SharedKernel.Application.Errors.ValidationError`, `public sealed class ValidationError : Error` with `public ValidationError(string propertyName, string errorMessage)` and `public string PropertyName { get; }`. Unchanged members — only the namespace moves.

- [ ] **Step 1: Create the file in its new home**

Content is the existing file with the namespace changed and the `<remarks>` rewritten — the old remarks justify the `SharedKernel.Web` location, which is no longer the reason it lives where it lives:

```csharp
using FluentResults;

namespace ModulithTemplate.SharedKernel.Application.Errors;

/// <summary>
/// A failed input validation, produced by the mediator's validation behaviour from a
/// FluentValidation failure and carried on a failed <see cref="Result"/>.
/// </summary>
/// <remarks>
/// In the Application layer so a handler can return one too, not only the behaviour — a uniqueness
/// check that reads as a field error should not have to invent its own error type. Blazor components
/// still reach it through their feature's Application project.
/// </remarks>
public sealed class ValidationError : Error
{
    /// <summary>Creates an error for one failed rule.</summary>
    /// <param name="propertyName">The command or query property the rule was declared on.</param>
    /// <param name="errorMessage">The rule's human-readable failure message.</param>
    public ValidationError(string propertyName, string errorMessage)
        : base(errorMessage)
    {
        PropertyName = propertyName;
        Metadata[nameof(PropertyName)] = propertyName;
    }

    /// <summary>The command or query property the failed rule was declared on.</summary>
    public string PropertyName { get; }
}
```

- [ ] **Step 2: Delete the old file and its folder**

```bash
cd /workspaces/modulith-template/working/content/modulith
rm -r src/SharedKernel/ModulithTemplate.SharedKernel.Web/Errors
```

- [ ] **Step 3: Give `SharedKernel.Application` the FluentResults reference and take it off `SharedKernel.Web`**

In `ModulithTemplate.SharedKernel.Application.csproj`, add to the existing `PackageReference` group:

```xml
    <PackageReference Include="FluentResults" />
```

In `ModulithTemplate.SharedKernel.Web.csproj`, remove `<PackageReference Include="FluentResults" />`. `ServiceScopeExtensions` — the only remaining type there — does not use it. Leave `Microsoft.Extensions.DependencyInjection.Abstractions`.

- [ ] **Step 4: Repoint every reference to the namespace**

Four `using`/`@using` lines. In `ValidationBehaviour.cs:8` and `ValidationBehaviourTests.cs:8`:

```csharp
using ModulithTemplate.SharedKernel.Application.Errors;
```

Check the `using` group ordering after the edit — `SharedKernel.Application` sorts before `SharedKernel.Web`, and `EnforceCodeStyleInBuild` will fail the build on a misordered group.

In `src/ModulithTemplate.Web/Components/_Imports.razor:10` and `src/Features/Orders/ModulithTemplate.Features.Orders.Web/Components/_Imports.razor:7`:

```razor
@using ModulithTemplate.SharedKernel.Application.Errors
```

In `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/Components/_Imports.razor:7` — note the different token:

```razor
@using ModulithApp.SharedKernel.Application.Errors
```

This last file is **not** compiled by the solution build. Only the scaffold run in Task 7 verifies it. Do not skip it because the build is green.

- [ ] **Step 5: Build and test**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && rtk proxy dotnet test
```

Expected: build clean, all tests pass including `MediatorPipelineRegistrationTests`.

If the build fails with `CS0246` in a `.razor` file, an `@using` was missed — grep for it:
`grep -rn "SharedKernel.Web.Errors" working/content/modulith working/content/feature`

---

### Task 3: Inline the ActivitySource name and subscribe by prefix

Do this *before* moving the behaviours. It severs `LoggingBehaviour -> ServiceDefaults`, which is the only thing making the behaviour move non-trivial: leaving it in place would create `SharedKernel.Application -> ServiceDefaults`, dragging ASP.NET Core and OpenTelemetry into every `Contracts` project.

**Files:**
- Delete: `working/content/modulith/src/ModulithTemplate.ServiceDefaults/ActivitySources.cs`
- Modify: `working/content/modulith/src/ModulithTemplate.ServiceDefaults/Extensions.cs` (header comment lines 4-5, and the `.AddSource` call at ~line 94)
- Modify: `working/content/modulith/src/ModulithTemplate.Web/Behaviours/LoggingBehaviour.cs` (usings + the `ActivitySource` field)
- Modify: `working/content/modulith/test/ModulithTemplate.WebTests/LoggingBehaviourTests.cs:34` (+ its `using`)
- Modify: `working/content/modulith/test/ModulithTemplate.WebTests/ServiceDefaultsTests.cs:84` (+ its `using`)

**Interfaces:**
- Produces: the string literal `"ModulithTemplate.Mediator"` as the mediator span source name, and `"ModulithTemplate.*"` as the OpenTelemetry subscription. Later tasks carry the first literal with `LoggingBehaviour` when it moves.

- [ ] **Step 1: Inline the name in `LoggingBehaviour`**

Replace the field at `LoggingBehaviour.cs`:

```csharp
    // Subscribed to by ConfigureOpenTelemetry's "ModulithTemplate.*" prefix, not by name.
    private static readonly ActivitySource ActivitySource = new("ModulithTemplate.Mediator");
```

Remove `using ModulithTemplate.ServiceDefaults;` from the file. Note the value drops the `Web` segment the old constant carried (`"ModulithTemplate.Web.Mediator"`) — the source is no longer web-specific.

- [ ] **Step 2: Switch the subscription to a prefix and delete the constant**

In `Extensions.cs`, replace the final `.AddSource(...)` call and its comment:

```csharp
                    // Every ActivitySource this solution defines. A prefix, not a list of names, so
                    // adding a source needs no change here — an unsubscribed one produces no spans.
                    .AddSource("ModulithTemplate.*");
```

Update the deviation list in the file header (lines 4-5) to match:

```csharp
//   * `.AddSource("ModulithTemplate.*")` added, so every ActivitySource this solution defines is
//     exported — currently the one LoggingBehaviour starts per mediator message.
```

Leave `tracing.AddSource(builder.Environment.ApplicationName)` at line 76 alone. It is now redundant (`ModulithTemplate.Web` matches the prefix), but it is upstream Aspire code and this file is deliberately kept as a small diff against upstream.

```bash
cd /workspaces/modulith-template/working/content/modulith
rm src/ModulithTemplate.ServiceDefaults/ActivitySources.cs
```

- [ ] **Step 3: Inline the literal in both tests**

`LoggingBehaviourTests.cs:34`:

```csharp
            ShouldListenTo = source => string.Equals(source.Name, "ModulithTemplate.Mediator", StringComparison.Ordinal),
```

`ServiceDefaultsTests.cs:84`:

```csharp
        using var source = new ActivitySource("ModulithTemplate.Mediator");
```

Remove `using ModulithTemplate.ServiceDefaults;` from `LoggingBehaviourTests.cs`. **Keep it in `ServiceDefaultsTests.cs`** — that file uses `AddServiceDefaults` and `MapDefaultEndpoints` from the same namespace.

Update the `<remarks>` on `ConfigureOpenTelemetry_subscribes_to_the_solution_activity_sources` — its role has narrowed from pinning one exact name to proving the prefix subscription works at all:

```csharp
    /// <remarks>
    /// The failure is silent: an unsubscribed <c>ActivitySource</c> looks present in the source but
    /// produces no spans. Starting a real activity is the only way to observe the subscription — the
    /// SDK exposes no list of the sources it listens to. Any <c>ModulithTemplate.*</c> name would do;
    /// this one is the name LoggingBehaviour uses.
    /// </remarks>
```

- [ ] **Step 4: Build and test**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && rtk proxy dotnet test
```

Expected: build clean, all tests pass. `ConfigureOpenTelemetry_subscribes_to_the_solution_activity_sources` passing is the proof the wildcard works — if it fails, OpenTelemetry's `AddSource` did not accept the `*` pattern and the whole telemetry decision needs revisiting. **Stop and report** if so.

---

### Task 4: Move the behaviours to `SharedKernel.Application`

A pure move plus a visibility widening. Task 3 removed the only dependency that made it awkward.

**Files:**
- Create: `working/content/modulith/src/SharedKernel/ModulithTemplate.SharedKernel.Application/Behaviours/LoggingBehaviour.cs`
- Create: `.../Behaviours/ExceptionBehaviour.cs`
- Create: `.../Behaviours/ValidationBehaviour.cs`
- Create: `.../Behaviours/BehaviourLog.cs`
- Delete: `working/content/modulith/src/ModulithTemplate.Web/Behaviours/` (all four files)
- Modify: `working/content/modulith/src/SharedKernel/ModulithTemplate.SharedKernel.Application/ModulithTemplate.SharedKernel.Application.csproj`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/Program.cs:2`
- Modify: `working/content/modulith/test/ModulithTemplate.WebTests/` — the `using` line in `LoggingBehaviourTests.cs`, `ExceptionBehaviourTests.cs`, `ValidationBehaviourTests.cs`, `MediatorPipelineRegistrationTests.cs`

**Interfaces:**
- Produces: `ModulithTemplate.SharedKernel.Application.Behaviours.{LoggingBehaviour,ExceptionBehaviour,ValidationBehaviour}<TMessage, TResponse>`, all `public sealed`, constructors and type constraints unchanged. `BehaviourLog` stays `internal static partial`.

- [ ] **Step 1: Move the four files**

```bash
cd /workspaces/modulith-template/working/content/modulith
mkdir -p src/SharedKernel/ModulithTemplate.SharedKernel.Application/Behaviours
mv src/ModulithTemplate.Web/Behaviours/*.cs src/SharedKernel/ModulithTemplate.SharedKernel.Application/Behaviours/
rmdir src/ModulithTemplate.Web/Behaviours
```

- [ ] **Step 2: Change the namespace and widen visibility in all four files**

In each file: `namespace ModulithTemplate.Web.Behaviours;` becomes

```csharp
namespace ModulithTemplate.SharedKernel.Application.Behaviours;
```

In the three behaviours only, `internal sealed class` becomes `public sealed class`. `BehaviourLog` stays `internal static partial class` — nothing outside the assembly names it.

- [ ] **Step 3: Add the usings the Web SDK was supplying implicitly**

`Microsoft.NET.Sdk.Web` implicitly imports `Microsoft.Extensions.Logging`; plain `Microsoft.NET.Sdk` does not. Add to the third-party `using` group of `BehaviourLog.cs`, `LoggingBehaviour.cs` and `ExceptionBehaviour.cs`:

```csharp
using Microsoft.Extensions.Logging;
```

`ValidationBehaviour.cs` takes no logger and needs nothing added.

- [ ] **Step 4: Update `SharedKernel.Application.csproj`**

It needs the packages the behaviours use. `FluentResults` was added in Task 2. Add:

```xml
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
```

Replace the leading comment — the existing one describes contents that do not exist ("the two event-handler interfaces, the published-contract marker, and the queue handlers"):

```xml
  <!-- What every feature's Application layer implements or injects, plus the mediator pipeline
       behaviours any host wires into its own AddMediator call.

       Feature Contracts projects reference this, so anything added here reaches them transitively. -->
```

`System.Diagnostics.ActivitySource` needs no package — it is in the shared framework. If the build says otherwise, stop and report rather than adding one.

- [ ] **Step 5: Update the host**

`Program.cs:2` — replace `using ModulithTemplate.Web.Behaviours;` with:

```csharp
using ModulithTemplate.SharedKernel.Application.Behaviours;
```

Check the `using` block order afterwards. The `options.PipelineBehaviors` list and its ordering comment are unchanged — the host still declares its own pipeline, which is the point of this whole change.

In `ModulithTemplate.Web.csproj`, remove `<PackageReference Include="FluentValidation" />` if nothing in the project still uses it (grep: `grep -rn "FluentValidation" src/ModulithTemplate.Web`). Keep `FluentResults` and `Mediator.Abstractions`.

Also check whether `<InternalsVisibleTo Include="ModulithTemplate.WebTests" />` is still needed — the behaviours were likely its only consumer. Remove it only if the test project still builds without it; re-add and move on if it does not.

- [ ] **Step 6: Update the four test files' usings**

In `LoggingBehaviourTests.cs`, `ExceptionBehaviourTests.cs`, `ValidationBehaviourTests.cs` and `MediatorPipelineRegistrationTests.cs`, replace `using ModulithTemplate.Web.Behaviours;` with:

```csharp
using ModulithTemplate.SharedKernel.Application.Behaviours;
```

- [ ] **Step 7: Build and test — this is the gate the whole plan exists for**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && rtk proxy dotnet test
```

Expected: build clean, all tests pass. `MediatorPipelineRegistrationTests` passing here is the proof that `AddMediator` still reaches behaviours in a referenced assembly — the assumption this whole change rests on. If it fails, **stop and report**; do not work around it.

---

### Task 5: New `SharedKernel.ApplicationTests` project

The behaviour tests now sit in a project named after a host that no longer owns them. The template's stated convention is one test project per project.

**Files:**
- Create: `working/content/modulith/test/SharedKernel/ModulithTemplate.SharedKernel.ApplicationTests/ModulithTemplate.SharedKernel.ApplicationTests.csproj`
- Move: `LoggingBehaviourTests.cs`, `ExceptionBehaviourTests.cs`, `ValidationBehaviourTests.cs` from `test/ModulithTemplate.WebTests/` into it
- Modify: `working/content/modulith/ModulithTemplate.slnx`
- Modify: `working/content/modulith/test/ModulithTemplate.WebTests/ModulithTemplate.WebTests.csproj`

**Interfaces:**
- Consumes: the `ModulithTemplate.SharedKernel.Application.Behaviours` and `.Errors` namespaces from Task 4 and Task 2.
- Produces: nothing other tasks depend on.

`MediatorPipelineRegistrationTests.cs` **stays in `WebTests`** — it exercises the generated `AddMediator` from the `ModulithTemplate.Web` assembly, which is the host's concern, not the shared project's.

- [ ] **Step 1: Create the test project**

`test/Directory.Build.props` supplies the target framework, xUnit, NSubstitute, `OutputType`, the MTP runner and `xunit.runner.json` (resolved via `$(MSBuildThisFileDirectory)`, so the extra directory level needs no change). The csproj holds only what is specific to it:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../src/SharedKernel/ModulithTemplate.SharedKernel.Application/ModulithTemplate.SharedKernel.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Diagnostics.Testing" />
  </ItemGroup>

</Project>
```

Three `../` — the project sits at `test/SharedKernel/<Project>/`, one level deeper than `test/ModulithTemplate.WebTests/`. `Mediator.Abstractions`, `FluentResults` and `FluentValidation` arrive transitively from `SharedKernel.Application`, so they are not listed.

- [ ] **Step 2: Register it in the solution**

In `ModulithTemplate.slnx`, add a folder after the existing `/test/` folder block and before `/test/Features/`:

```xml
  <Folder Name="/test/SharedKernel/">
    <Project Path="test/SharedKernel/ModulithTemplate.SharedKernel.ApplicationTests/ModulithTemplate.SharedKernel.ApplicationTests.csproj" />
  </Folder>
```

- [ ] **Step 3: Move the three test files**

```bash
cd /workspaces/modulith-template/working/content/modulith
mv test/ModulithTemplate.WebTests/LoggingBehaviourTests.cs \
   test/ModulithTemplate.WebTests/ExceptionBehaviourTests.cs \
   test/ModulithTemplate.WebTests/ValidationBehaviourTests.cs \
       test/SharedKernel/ModulithTemplate.SharedKernel.ApplicationTests/
```

Change the namespace in each from `ModulithTemplate.WebTests` to:

```csharp
namespace ModulithTemplate.SharedKernel.ApplicationTests;
```

- [ ] **Step 4: Take the now-unused package off `WebTests`**

`Microsoft.Extensions.Diagnostics.Testing` supplies `FakeLogger`, used only by `LoggingBehaviourTests`. Confirm nothing left in `WebTests` needs it:

```bash
grep -rn "Logging.Testing\|FakeLogger" test/ModulithTemplate.WebTests
```

If that returns nothing, remove the `PackageReference` from `ModulithTemplate.WebTests.csproj`.

- [ ] **Step 5: Build and test, and check the new project's tests actually ran**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && rtk proxy dotnet test --project test/SharedKernel/ModulithTemplate.SharedKernel.ApplicationTests
rtk proxy dotnet test
```

Expected: build clean; the targeted run reports a non-zero test count (a zero-test run is a failure, not a pass — see Global Constraints); the full run is green.

---

### Task 6: Documentation and stale comments

Three comments now describe a world that no longer exists, and one convention became undiscoverable.

**Files:**
- Modify: `working/content/modulith/CLAUDE.md:116-118`, and add one line to the same section
- Modify: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Contracts/ModulithTemplate.Features.Orders.Contracts.csproj`
- Modify: `working/content/feature/` — the equivalent `Contracts.csproj` comment, if it carries the same text

- [ ] **Step 1: Fix the `ValidationError` path in the generated CLAUDE.md**

At `working/content/modulith/CLAUDE.md:117`, replace the parenthesised path:

```markdown
(`ModulithTemplate.SharedKernel.Application/Errors/ValidationError.cs`)
```

- [ ] **Step 2: Record the ActivitySource naming convention**

With `ActivitySources.cs` gone, nothing tells a reader that a new source must be named under the exported prefix. Add to the same CLAUDE.md section that discusses the pipeline (after the validation paragraph ending "bind the messages to their fields."):

```markdown
Telemetry from the pipeline rides on an `ActivitySource` named `ModulithTemplate.Mediator`.
`ConfigureOpenTelemetry` subscribes to `ModulithTemplate.*`, so any new source must carry that
prefix — one named otherwise produces no spans, silently.
```

- [ ] **Step 3: Reword the Contracts isolation comment**

`Orders.Contracts.csproj`'s comment says it "deliberately references nothing but SharedKernel.Application". Still true, but that project now carries FluentResults, FluentValidation and Logging.Abstractions transitively. Append one sentence to the existing comment rather than rewriting it:

```xml
       SharedKernel.Application now also carries the pipeline behaviours, so their packages arrive
       here transitively — harmless, but this project should still use none of them.
```

Check `working/content/feature/` for the same comment text and apply the same edit if present:
`grep -rn "back door into the module" working/content/feature`

- [ ] **Step 4: Verify the docs did not break the build**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
```

Expected: clean. (Markdown cannot break it; the csproj comment can, if the XML comment is malformed.)

---

### Task 7: End-to-end scaffold verification

The feature sub-template's `_Imports.razor` edit from Task 2 is invisible to every build so far. Only a real scaffold run proves it.

**Files:** none modified — this task is verification. Any failure sends you back to the task that caused it.

- [ ] **Step 1: Scaffold a solution from the template**

```bash
cd /workspaces/modulith-template
rm -rf /tmp/MyApp
dotnet new install working/content/modulith
dotnet new modulith -n MyApp -o /tmp/MyApp
```

- [ ] **Step 2: Confirm no template token survived**

```bash
grep -rn "ModulithTemplate" /tmp/MyApp --include=* -l | grep -v -e /obj/ -e /bin/
```

Expected: **no output**. A hit means an identifier or string literal lost its `sourceName` prefix — most likely the `"ModulithTemplate.Mediator"` or `"ModulithTemplate.*"` literals introduced in Task 3, which must have become `MyApp.Mediator` and `MyApp.*`. Verify those two explicitly:

```bash
grep -rn "MyApp.Mediator\|MyApp\.\*" /tmp/MyApp --include=*.cs
```

Expected: the `ActivitySource` field in `LoggingBehaviour.cs`, the `AddSource` call in `Extensions.cs`, and the two test literals.

- [ ] **Step 3: Build and test the scaffolded solution**

```bash
cd /tmp/MyApp
dotnet build MyApp.slnx -warnaserror
rtk proxy dotnet test
```

Expected: clean build, green tests, non-zero test count.

- [ ] **Step 4: Scaffold a feature into it**

```bash
cd /workspaces/modulith-template
dotnet new install working/content/feature
cd /tmp/MyApp
dotnet new modulith-feature --appName MyApp -n Payments
dotnet build MyApp.slnx -warnaserror
```

Expected: clean build. A `CS0246` inside a generated `Payments.Web` Razor file means the `_Imports.razor` edit in Task 2 Step 4 was missed or used the wrong token.

```bash
grep -rn "FeatureName\|ModulithApp" /tmp/MyApp -l | grep -v -e /obj/ -e /bin/
```

Expected: no output.

- [ ] **Step 5: Confirm the mediator span still reaches the collector**

The wildcard subscription is the one change no unit test fully proves. From the devcontainer (Postgres running):

```bash
cd /tmp/MyApp/src/MyApp.Web && dotnet run
```

Exercise a command through the UI — including one that fails validation — and confirm in the collector that each command produces one `MyApp.Mediator` span with the Npgsql spans nested under it, and that the failing one is marked `Error`.

- [ ] **Step 6: Uninstall the local templates**

```bash
cd /workspaces/modulith-template
dotnet new uninstall working/content/modulith
dotnet new uninstall working/content/feature
rm -rf /tmp/MyApp
```

- [ ] **Step 7: Final gate**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && rtk proxy dotnet test
```

Expected: clean build, all tests pass. Report the test count so the maintainer can compare it against the Task 1 baseline — it should be the baseline plus one (`MediatorPipelineRegistrationTests`).

---

## Observed but out of scope

- `working/content/modulith/CLAUDE.md:143` claims `@using Mediator` and `@using ModulithTemplate.SharedKernel.Web.Extensions` are "neither in `_Imports.razor`" — both are, at lines 9 and 11 of the host's `_Imports.razor` and lines 6 and 8 of the Orders one. Pre-existing inaccuracy, adjacent to Task 6's edits but a separate claim. Flag to the maintainer; do not fix silently.
- Architecture-test coverage of the `SharedKernel.*` projects. `SolutionAssemblies` loads only assemblies containing `.Features.`, and `NamingConventionTests` documents that shared projects are deliberately excluded because they define abstractions with the same suffixes. So nothing this plan does trips an existing rule, and extending ArchUnitNET to the shared projects is a separate, larger change.

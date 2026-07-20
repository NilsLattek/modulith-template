# Feature layer dependency architecture test

Date: 2026-07-20
Status: Approved (design)

## Context

The template content (`working/content/modulith/`) is a Vertical Slice + DDD modular
monolith. Each feature is a folder under `src/Features/<Feature>/` containing four layer
projects:

- `ModulithTemplate.Features.<Feature>.Domain`
- `ModulithTemplate.Features.<Feature>.Application`
- `ModulithTemplate.Features.<Feature>.Infrastructure`
- `ModulithTemplate.Features.<Feature>.Web`

An existing architecture test, `FeatureModuleTests.ModulesCannotDependOnEachOther`
(`working/content/modulith/test/ModulithTemplate.ArchitectureTests/FeatureModuleTests.cs`),
uses ArchUnitNET (`TngTech.ArchUnitNET.xUnitV3` 0.13.3) to assert that features do not
depend on each other across module boundaries. It loads every `*.dll` under the solution
directory whose path contains `.Features.` and slices by namespace.

This spec adds a second, complementary test that enforces the **directional layering
rules inside a feature**, and does so generically so that new features are covered
automatically.

## Goal

Enforce these intra-feature dependency rules:

| Layer          | May depend on            | Must NOT depend on                     |
|----------------|--------------------------|----------------------------------------|
| Domain         | (nothing)                | Application, Infrastructure, Web        |
| Application    | Domain                   | Infrastructure, Web                     |
| Infrastructure | Domain                   | Application, Web                        |
| Web            | Application, Infrastructure | Domain (directly)                    |

"May depend on" describes what is *allowed*, not *required*. The test only asserts the
absence of forbidden dependencies. (Today `Orders.Web` references Application but not
Infrastructure — that is compliant.)

## Key design decision: match by assembly name, not namespace

The Domain project currently ships a placeholder type in
`namespace ModulithTemplate.Features.Example` (see
`src/Features/Orders/.../Domain/Entities/SomeEntity.cs`) — it does **not** carry a
`.Domain` namespace suffix. A namespace-based layer matcher would therefore silently fail
to identify the Domain layer, and the rule would pass vacuously (a false green).

Assembly names equal project names, and the template's `sourceName` mechanics require
every project to keep its `ModulithTemplate.Features.<Feature>.<Layer>` name. Assembly
name is therefore the reliable layer signal. Layers are matched with a regex on assembly
name, e.g. `.*\.Features\..*\.Domain$`.

ArchUnitNET 0.13.3 supports this via `ResideInAssemblyMatching(pattern)` and
`NotDependOnAnyTypesThat().ResideInAssemblyMatching(pattern)` (verified in the packaged
`ArchUnitNET.dll`).

## Scope

Only `ModulithTemplate.Features.*` assemblies are constrained. The `.Features.` segment in
the regex automatically excludes the root/shared projects
(`ModulithTemplate.Infrastructure`, `ModulithTemplate.Web`, `ModulithTemplate.Web.Common`,
`ModulithTemplate.FeatureCore`), which act as composition root / shared kernel.

## Design

### Ensuring the feature assemblies are built (project references)

The tests discover layers by scanning the solution tree for compiled `*.dll` files whose
name contains `.Features.`. That only works if those assemblies have actually been built.
In this template the feature projects are **orphans in the reference graph** — nothing
references them (the host `ModulithTemplate.Web` references only `ModulithTemplate.Web.Common`;
`ModulithTemplate.Infrastructure` references only `ModulithTemplate.FeatureCore`). Consequently
only a full-solution build (`dotnet build ModulithTemplate.slnx`) produces every feature
assembly. Building or running the `ModulithTemplate.ArchitectureTests` project on its own
built nothing feature-related, so the disk scan saw only whatever DLLs a prior build happened
to leave behind (e.g. `Domain` and `Infrastructure` but not `Application`/`Web`), and the
per-layer presence guard failed.

To make the tests deterministic regardless of how they are invoked, the ArchitectureTests
project references every feature project via a glob, so they are always built alongside the
tests and new features are picked up automatically with no edits here:

```xml
<ItemGroup>
  <ProjectReference Include="../../src/Features/**/*.csproj" />
</ItemGroup>
```

This keeps the test generic (no per-feature edits) and carries no `ModulithTemplate` token,
so it survives the scaffold rename. The presence guard (below) remains as defense in depth:
if a feature assembly is ever missing despite this, the affected fact fails loudly instead
of passing vacuously.

### Shared assembly loader

Extract the assembly discovery + loading currently inline in `FeatureModuleTests`
(`GetSolutionDirectory` and the `.Features.`-filtered `Assembly.LoadFile` loop) into a
small internal helper, e.g. `SolutionAssemblies`, in the ArchitectureTests project. Behavior
is kept identical (including the `BadImageFormatException` swallow). Refactor
`FeatureModuleTests` to consume the helper, and use it from the new test too. This removes
duplication rather than copy-pasting the loader.

### New test: `FeatureLayerTests`

New file `FeatureLayerTests.cs` in the same test project. No new package references.

A single data-driven table of layer rules drives four thin `[Fact]` methods:

```csharp
private sealed record LayerRule(string Name, string Suffix, string[] Forbidden);

private static readonly LayerRule[] Rules =
[
    new("Domain",         "Domain",         ["Application", "Infrastructure", "Web"]),
    new("Application",    "Application",    ["Infrastructure", "Web"]),
    new("Infrastructure", "Infrastructure", ["Application", "Web"]),
    new("Web",            "Web",            ["Domain"]),
];
```

Facts (one per layer, for granular failure attribution):

- `Domain_depends_on_no_other_layer`
- `Application_depends_only_on_Domain`
- `Infrastructure_depends_only_on_Domain`
- `Web_does_not_access_Domain_directly`

Each calls a shared helper `AssertLayerRule(LayerRule rule)` that:

1. Builds the feature-scoped assembly-name regex for the layer suffix, e.g.
   `FeaturePattern("Domain")` => `.*\.Features\..*\.Domain$`.
2. Guards against a silent no-op: asserts the layer's own type set is non-empty
   (fails loudly if naming drift ever stops the layer from being discovered).
3. Builds and checks the rule:

```csharp
Types().That().ResideInAssemblyMatching(FeaturePattern(rule.Suffix))
    .Should().NotDependOnAnyTypesThat()
    .ResideInAssemblyMatching(FeaturePattern(forbiddenAlternation))
    .Check(architecture);
```

where `forbiddenAlternation` matches any of the forbidden suffixes, e.g.
`.*\.Features\..*\.(Application|Infrastructure|Web)$`.

The `Architecture` is built once from the shared loader's assemblies (a cached static, as
the existing test builds its own; reuse the same pattern).

### Genericity

Feature-generality comes from the `.*\.Features\..*` portion of the regex: any new feature
folder adds assemblies matching the same patterns and is covered with zero test edits.
Adding a *new layer* later is a one-row + one-Fact change.

## Testing / verification

- `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror` (must stay
  warning-clean).
- `dotnet test working/content/modulith/ModulithTemplate.slnx` — the four new facts pass
  against the current compliant Orders feature, and the existing
  `ModulesCannotDependOnEachOther` still passes after the loader refactor.
- Sanity: temporarily introduce a forbidden reference (e.g. Domain -> Application) and
  confirm the corresponding fact fails, then revert.

## Out of scope

- Root/shared project layering.
- Requiring (as opposed to permitting) specific dependencies.
- Changing the existing cross-feature rule's semantics.

## Notes

- Per this repository's `CLAUDE.md`, no git actions are taken by the assistant; the
  maintainer commits.
- This spec lives at the repo root (`docs/superpowers/specs/`) and is intentionally **not**
  under `working/content/modulith/`, so it does not ship into scaffolded projects.

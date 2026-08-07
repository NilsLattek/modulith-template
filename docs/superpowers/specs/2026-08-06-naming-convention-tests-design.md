# Naming convention architecture tests

Date: 2026-08-06
Status: Approved (design)

## Context

The template content (`working/content/modulith/`) already carries two ArchUnitNET test
classes in `test/ModulithTemplate.ArchitectureTests/`:

- `FeatureModuleTests` — features must not depend on each other.
- `FeatureLayerTests` — directional layering rules inside a feature.

Both consume the shared `SolutionAssemblies` helper, which discovers compiled feature-layer
assemblies (file name contains `.Features.`, excluding `*Tests`) and builds a cached
`Architecture` from them.

This spec adds a third class enforcing **naming and placement conventions** for the four
building blocks the generated project's `CLAUDE.md` describes but does not currently
mechanically enforce: specifications, mappers, app services, and domain services.

The starting point was a test copied from another solution (`MyProject`) that hard-codes
literal namespaces:

```csharp
Classes().That().ResideInNamespace("MyProject.Domain.Specifications")
    .Should().HaveNameEndingWith("Spec");
```

That shape does not transfer directly. In this template there is no single `Domain`
namespace — every feature owns its own (`ModulithTemplate.Features.Orders.Domain`), and the
`ModulithTemplate` token is renamed at scaffold time. Literal namespaces would both miss
every feature but one and leak the template's name into generated projects. The rules must
be regex/pattern based, exactly as `FeatureLayerTests.FeaturePattern` already is.

## Goal

Enforce four conventions, each in **both directions**:

| Concept        | Home namespace (per feature)  | Type suffix     |
|----------------|-------------------------------|-----------------|
| Specification  | `<Feature>.Domain.Specifications` | `Spec`      |
| Mapper         | `<Feature>.Application.Mappers`    | `Mapper`   |
| App service    | `<Feature>.Application.Services`   | `AppService` |
| Domain service | `<Feature>.Domain.Services`        | `DomainService` |

For each row, two rules:

1. **Naming** — every type in the home namespace has the suffix.
2. **Placement** — every type with the suffix resides in the home namespace.

Eight rules total. The naming direction catches a badly-named class in the right folder;
the placement direction catches a well-named class in the wrong one. The placement
direction is the more likely mistake and is not implied by the folder layout alone.

## Key design decision: accept vacuity

None of these four concepts exists in the scaffolded `Orders` sample feature. Its complete
source is `SomeEntity`, `IOrdersRepository`, `OrdersContext`, `OrdersRepository`,
`OrdersModule`, and two `Configuration.cs` files. All eight rules therefore match zero
types in a freshly generated solution.

**This does not pass by itself.** ArchUnitNET 0.13.3 treats a rule whose predicate matches
nothing as a failure, not a pass, and throws:

```
The rule requires positive evaluation, not just absence of violations.
Use WithoutRequiringPositiveResults() or improve your rule's predicates.
```

So both helpers must append `.WithoutRequiringPositiveResults()`. Without it every freshly
scaffolded solution would fail its own architecture tests on day one — the opposite of the
intent here. The call suppresses **only** the zero-match case; a rule that matches types and
finds a real violation still fails normally. That was verified in both directions during
implementation, and again against a freshly scaffolded project, where all eight facts pass
with zero matches.

Accepting vacuity is deliberate and is **not** treated as a defect, unlike in `FeatureLayerTests` and
`FeatureModuleTests`, where explicit `Assert` guards exist precisely to prevent vacuous
passes. The distinction:

- A **layering** rule that matches nothing means the architecture is not being checked at
  all — a false green over code that exists.
- A **naming** rule that matches nothing means the user has not written a specification
  yet. It is a conditional tripwire armed for code that does not exist yet, which is the
  normal and correct state for a template.

Consequently no presence guards are added, and the class carries an XML comment recording
this reasoning so a future reader does not "fix" it by copying the guards from its
neighbours.

The consequence to manage is that the rules cannot be validated by simply running them —
see "Testing / verification".

## Design

### New test: `NamingConventionTests`

New file `NamingConventionTests.cs` in `test/ModulithTemplate.ArchitectureTests/`. No csproj
change and no new package references: `TngTech.ArchUnitNET.xUnitV3` 0.13.3 is already
referenced, and `SolutionAssemblies.Architecture` already exposes the right assembly set.

Following the data-driven shape of `FeatureLayerTests`, a table drives thin `[Fact]`s:

```csharp
private sealed record NamingConvention(string Name, string NamespaceSuffix, string TypeSuffix);

private static readonly NamingConvention[] Conventions =
[
    new("Specification",  "Domain.Specifications", "Spec"),
    new("Mapper",         "Application.Mappers",   "Mapper"),
    new("App service",    "Application.Services",  "AppService"),
    new("Domain service", "Domain.Services",       "DomainService"),
];
```

Eight facts, one per rule, for granular failure attribution:

- `Types_in_Specifications_end_with_Spec` / `Types_named_Spec_reside_in_Specifications`
- `Types_in_Mappers_end_with_Mapper` / `Types_named_Mapper_reside_in_Mappers`
- `Types_in_Application_Services_end_with_AppService` / `Types_named_AppService_reside_in_Application_Services`
- `Types_in_Domain_Services_end_with_DomainService` / `Types_named_DomainService_reside_in_Domain_Services`

Each delegates to one of two helpers, `AssertNaming(string conventionName)` and
`AssertPlacement(string conventionName)`, which look the row up out of the table and build:

```csharp
// naming
Types().That().ResideInNamespaceMatching(pattern).And().AreNotNested()
    .Should().HaveNameEndingWith(convention.TypeSuffix)
    .Because(...)
    .WithoutRequiringPositiveResults()

// placement
Types().That().HaveNameEndingWith(convention.TypeSuffix).And().AreNotNested()
    .Should().ResideInNamespaceMatching(pattern)
    .Because(...)
    .WithoutRequiringPositiveResults()
```

`.WithoutRequiringPositiveResults()` is required rather than stylistic — see "accept
vacuity" above.

`ResideInNamespaceMatching`, `HaveNameEndingWith`, `AreNotNested` and the `Should()`-side
inverses were verified present in the packaged `ArchUnitNET.dll` 0.13.3.

### Namespace pattern

Built by a private helper mirroring `FeatureLayerTests.FeaturePattern`:

```csharp
private static string FeatureNamespacePattern(string namespaceSuffix) =>
    @".*\.Features\..*\." + Regex.Escape(namespaceSuffix) + @"(\..*)?$";
```

e.g. `Domain.Specifications` becomes `.*\.Features\..*\.Domain\.Specifications(\..*)?$`.

The trailing `(\..*)?` deliberately permits sub-namespaces
(`...Domain.Specifications.Pricing` is in scope), while still rejecting a sibling namespace
whose name merely starts with the same text (`...Domain.SpecificationsExtra` does not
match, because after `Specifications` the pattern requires either end-of-string or a dot).

Feature-generality comes from the `.*\.Features\..*` portion: a new feature is covered with
zero test edits, and no `ModulithTemplate` token appears, so the file survives the scaffold
rename.

### Type-name pattern: generic arity

The suffix checks cannot use `HaveNameEndingWith(suffix)`. ArchUnitNET's `IType.Name` carries
the CLR **arity suffix** for generic types — a generic `ByIdSpec<T>` reports its name as
`` ByIdSpec`1 ``, not `ByIdSpec`. Verified against this solution's own assemblies:
`` OrdersRepository`1 ``, `` IOrdersRepository`1 ``.

Left unhandled this breaks both directions, in opposite and equally bad ways:

- **Naming** — a correctly-named `ActiveOrdersSpec<T>` in `Domain/Specifications/` *fails* the
  rule, with a message that reads like a tooling bug. Generic specifications are an ordinary
  Ardalis.Specification pattern, and this template's own `IOrdersRepository<T>` shows generics
  are idiomatic here.
- **Placement** — `That().HaveNameEndingWith("Spec")` never selects a generic `` *Spec`1 `` at
  all, so a *misplaced* generic specification is not caught. Combined with
  `.WithoutRequiringPositiveResults()`, that zero selection is indistinguishable from a pass.
  This is the one case where the vacuous-tripwire design's guarantee genuinely fails, which is
  why it is fixed in code rather than documented as a limitation.

Both sides therefore use a second pattern helper:

```csharp
private static string TypeNamePattern(string typeSuffix) =>
    ".*" + Regex.Escape(typeSuffix) + @"(`\d+)?$";
```

The optional trailing group admits the arity suffix without weakening the match for ordinary
non-generic types.

### `Types()` rather than `Classes()`

Interfaces participate in these conventions — `IOrdersAppService` should live in
`Application.Services` alongside its implementation — so the rules use `Types()`.

### `.AreNotNested()` is insurance, not load-bearing

The original rationale for this filter was that lambda closures (`<>c__DisplayClass`) and
async state machines are nested compiler-generated types reporting the *enclosing*
namespace, so the naming direction would fail the first time a user wrote a LINQ expression
inside a `*Spec` class.

**That rationale was tested during implementation and does not hold on 0.13.3.** A `*Spec`
fixture containing a closure was compiled, reflection confirmed
`SomeEntityByIdSpec+<>c__DisplayClass0_0` genuinely exists in the emitted assembly in the
`Domain.Specifications` namespace, and the naming rule passed *with the filter removed*.
ArchUnitNET excludes compiler-generated nested types from its type universe itself.

The filter is retained anyway, as insurance against **hand-written** nested types — a public
nested helper inside a `*Spec` class would otherwise trip the naming rule for no useful
reason. It is cheap and harmless, but it is not what makes these rules work, and removing it
would not break them.

Mapperly needs no special handling: its source generator emits into the same `partial`
class it was applied to, producing no additional top-level type.

### Scope

Only feature-layer assemblies are constrained, and this requires no explicit exclusion:
`SolutionAssemblies` already restricts the architecture to assemblies whose file name
contains `.Features.` and does not end in `Tests`. A `*Mapper` in the host
(`ModulithTemplate.Web`) or in the shared kernel (`ModulithTemplate.FeatureCore`) is
outside the loaded architecture entirely and cannot trip the placement rules.

The four suffixes do not collide with each other: no suffix in the table is itself a
suffix of another (`DomainService` and `AppService` share only `Service`, which is not a
rule).

## Documentation changes

`working/content/modulith/CLAUDE.md` currently contradicts the convention being enforced
and must be updated in the same change, otherwise the generated project's guidance and its
own tests disagree:

1. **Domain bullet** — currently mentions `Specifications/` as `*Spec` (already correct)
   but places domain services with no folder. Add `Services/` as the home for
   `*DomainService`.
2. **Application bullet** — currently reads "`Mapper/*Mapper.cs`". Change to `Mappers/`,
   and state that app services live in `Services/`.
3. **Architecture paragraph** — currently says `ModulithTemplate.ArchitectureTests`
   enforces that feature modules must not depend on each other. Extend it to note that the
   same project also enforces the naming/placement conventions above.

## Testing / verification

Because all eight rules are vacuous on a fresh scaffold, a green test run proves nothing —
a typo in a namespace regex is indistinguishable from a correct rule. The rules must
therefore be exercised against real types before the change is considered done:

1. `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror` — must stay
   warning-clean.
2. `cd working/content/modulith && dotnet test` — all existing tests plus the eight new
   facts pass.
3. **Compliance check**: temporarily add one conforming type per convention to the Orders
   feature (e.g. `Orders.Domain/Specifications/SomeEntityByIdSpec.cs`,
   `Orders.Application/Mappers/SomeEntityMapper.cs`,
   `Orders.Application/Services/IOrdersAppService.cs`,
   `Orders.Domain/Services/OrdersDomainService.cs`). Re-run; all eight facts must still
   pass — this proves the namespace patterns actually match, rather than matching nothing.
4. **Violation check**: mutate each temporary type to break its rule — rename
   `SomeEntityByIdSpec` to `SomeEntityById` (naming direction) and move a `*Spec` up to the
   `Domain` namespace (placement direction) — confirming the corresponding fact fails with
   a readable message. Repeat per convention.
5. Remove all temporary types, re-run, confirm green.
6. Scaffold end-to-end and confirm no `ModulithTemplate` token survives in the new file:
   `dotnet new install working/content/modulith`, `dotnet new modulith -n MyApp -o /tmp/MyApp`,
   then grep `/tmp/MyApp` for `ModulithTemplate`.

## Out of scope

- **Seeding the `Orders` sample** with example specifications, mappers, app services or
  domain services. This was considered and rejected: it would change what every generated
  project contains, which is a larger product decision than adding a test.
- **The `modulith-feature` sub-template** (`working/content/feature/`) gains no new folders.
  Empty directories do not survive scaffolding and there is nothing to place in them.
- Conventions with live examples in the scaffold (`*Context`, `*Repository`,
  `I<Feature>Repository`, `*Module`). These are enforced implicitly by the code compiling
  and were left out to keep the change focused.
- Rules beyond naming and placement — visibility (`internal` implementations), attribute
  presence (`[Mapper]`), or base-type requirements (`Specification<T>`).

## Notes

- Per this repository's `CLAUDE.md`, no git actions are taken by the assistant; the
  maintainer commits.
- This spec lives at the repo root (`docs/superpowers/specs/`) and is intentionally **not**
  under `working/content/modulith/`, so it does not ship into scaffolded projects.

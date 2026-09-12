# 05: Architecture rules for integration events

**What to build:** the build fails when an Integration Event is declared somewhere that would
reintroduce the coupling this whole mechanism exists to remove.

An Integration Event declared in a Feature's `Domain` or `Application` rather than its `Contracts`
still compiles and still works, but a consumer referencing it would take a dependency on the
publishing Feature's internals — and the cross-Feature isolation rule cannot see it, because it
deliberately ignores `Contracts` types. This is the same gap the contract isolation tests were
written to close.

Follow the existing convention tests: assert in both directions, so a misplaced type fails just as a
correctly placed but wrongly named one does, and guard against the rule passing vacuously when no
Integration Event is discovered.

**Blocked by:** 03 (Orders publishes an integration event, Payments consumes it).

**Status:** done

- [x] An Integration Event outside a `Contracts` assembly fails the build
- [x] Placement and naming conventions for Integration Events are enforced in both directions
- [x] The rules fail loudly rather than passing vacuously if no Integration Event is found
- [x] `dotnet test` passes with the sample event in place

## Comments

**Implemented** on `claude/awesome-ramanujan-54ymto`.

`IntegrationEventTests` holds the rules the naming table cannot express, keyed on the interface
rather than the name:

- **Placement**: every type implementing `IIntegrationEvent` resides in a `*.Contracts` assembly.
  This is the rule the ticket is about — `ContractIsolationTests` exempts Contracts types from the
  cross-feature rules by design, so an event declared in `Domain` or `Application` is invisible to it.
- **Naming, forward**: every `IIntegrationEvent` is named `*IntegrationEvent`.
- **Naming, reverse**: every type named `*IntegrationEvent` in a feature assembly implements
  `IIntegrationEvent` — otherwise it carries no `EventId`/`GroupKey`, cannot be staged, and the two
  rules above never look at it.

`NamingConventionTests` gained the namespace half in both directions: `*IntegrationEvent` →
`Contracts.Events`, `*IntegrationEventHandler` → `Application.IntegrationEventHandlers`, plus the
matching content rules.

**The vacuity guard is the interesting part.** The naming table passes on an empty match set by
design (`WithoutRequiringPositiveResults` — a fresh scaffold has no specifications yet), so on its
own, deleting the sample event would disarm all of this silently. Each test in
`IntegrationEventTests` therefore asserts first that at least one `IIntegrationEvent` was
discovered, and fails naming that as the reason.

### Verified by mutation, not just by a green run

Four deliberate breakages, each caught by exactly the rule meant for it:

| Mutation | Failed |
| --- | --- |
| `MutantIntegrationEvent` in `Orders.Application` | placement rule + `Types_carrying_a_convention_suffix...` |
| `MutantThing : IIntegrationEvent` in `Contracts/Events` | naming rule + `Types_in_a_convention_namespace...` |
| `MutantImposterIntegrationEvent` implementing nothing | reverse naming rule |
| No event discoverable at all | all three, with the vacuity message |

### One thing a self-review changed

The reverse rule first scoped itself with a hardcoded five-layer alternation, which a sixth layer
would have silently escaped. It needed *some* assembly filter — `IIntegrationEvent` itself ends in
`IntegrationEvent` and does not implement itself, so an unscoped rule fails on the abstraction it
enforces. `NamingConventionTests` already had a private pattern for exactly this; it moved to
`SolutionAssemblies.AnyFeatureLayerPattern` and both files now share it.

`dotnet build -warnaserror` clean; `dotnet test` 133/133. A scaffolded `MyApp` builds clean and
passes 127/127 (the six atomicity tests do not ship), with no `ModulithTemplate` token surviving.

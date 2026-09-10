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

**Status:** ready-for-agent

- [ ] An Integration Event outside a `Contracts` assembly fails the build
- [ ] Placement and naming conventions for Integration Events are enforced in both directions
- [ ] The rules fail loudly rather than passing vacuously if no Integration Event is found
- [ ] `dotnet test` passes with the sample event in place

# 02: Payments Feature

**What to build:** the generated solution contains a second Feature, `Payments`, alongside `Orders`.
It has its own schema, entity, migration and registration with the host, and does not yet publish or
consume anything.

This exists because cross-Feature communication cannot be demonstrated with one Feature, and because
the cross-Feature isolation rule currently passes against a single slice — a second Feature is the
first real test of it.

Scaffold it with the existing `modulith-feature` sub-template rather than by hand, so any gap in that
template surfaces here rather than after the mechanism is built on top of it.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] `Payments` is scaffolded with `dotnet new modulith-feature` and its projects are in the solution
- [ ] It owns an entity, a schema and a migration that `update-database.sh` applies
- [ ] It is registered with the host, including the project reference and configuration call the
      sub-template does not add
- [ ] The cross-Feature isolation and layering tests pass with two Features present
- [ ] Nothing named `FeatureName` or `ModulithApp` survives in the scaffolded output
- [ ] `dotnet build -warnaserror` is clean and `dotnet test` passes

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

- [x] `Payments` is scaffolded with `dotnet new modulith-feature` and its projects are in the solution
- [x] It owns an entity, a schema and a migration that `update-database.sh` applies
- [x] It is registered with the host, including the project reference and configuration call the
      sub-template does not add
- [x] The cross-Feature isolation and layering tests pass with two Features present
- [x] Nothing named `FeatureName` or `ModulithApp` survives in the scaffolded output
- [x] `dotnet build -warnaserror` is clean and `dotnet test` passes

## Comments

Implemented on `claude/awesome-ramanujan-54ymto`.

- Scaffolded with `dotnet new modulith-feature --appName ModulithTemplate -n Payments`; the
  post-action registered all nine projects in `ModulithTemplate.slnx` (folder order tidied by hand
  to match the existing convention).
- `Payment` entity (`OrderId`, `Amount`) with invariants on the order id, positivity, and both the
  scale and the precision of the `numeric(18,2)` column, so an amount the database would reject is
  refused by the domain instead of failing at `SaveChangesAsync`.
- Host registration — the part the sub-template does not add — is the `ProjectReference` in
  `ModulithTemplate.Web.csproj` and `builder.ConfigurePaymentsFeature();` in `Program.cs`.
  `dotnet ef dbcontext list` now reports both contexts, so `update-database.sh` picks Payments up.
- `20260911193049_InitialPayments` verified for real against `postgres:18.3`: `update-database.sh`
  created `payments.payment` with `numeric(18,2)` and the `order_id` index.
- Cross-Feature isolation confirmed non-vacuous: temporarily referencing `Orders.Domain` from
  `Payments.Domain` made `FeatureModuleTests.ModulesCannotDependOnEachOther` fail, and reverting it
  made the suite green again.
- `dotnet build -warnaserror` clean; `dotnet test` 93/93. A scaffolded `MyApp` also builds clean and
  passes 93/93, with no `ModulithTemplate`/`ModulithApp`/`FeatureName` token surviving.

Follow-ups, not done here:

- `Orders` still ships **no** migration while `Payments` now ships one. Both READMEs say so, but the
  asymmetry is worth a deliberate decision — either scaffold an `Orders` migration too, or drop the
  Payments one and leave migrations entirely to the user.
- The `Payments` name previously used as the *example* in `CLAUDE.md` and the generated `README.md`
  now collides with the real feature; both were changed to `Shipping`.

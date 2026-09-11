# 01: Outbox storage foundation

**What to build:** the shared outbox table exists in the generated solution's database and deploys
through the same migration commands as every Feature schema. Nothing publishes to it yet — this is
the storage and tooling the rest of the work stands on.

A new SharedKernel-level project owns the outbox `DbContext` and its migration. The context maps the
library's message entity into a schema of its own, and the connection declares that schema on its
search path, because the library's claim SQL names its tables unqualified. Registering the context
with the host is what makes `update-database.sh` and CI's pending-model-changes check discover it,
since both enumerate contexts from the host's DI container rather than a hard-coded list.

The migration wrappers assume a Feature-shaped path, so `add-migration.sh` needs a case for this
context, and the release workflow's per-schema script loop needs the shared schema added.

**Blocked by:** None (can start immediately).

**Dependency:** `Underground.Outbox` 0.16.0 is published and already pinned in central package
management; this ticket adds the `PackageReference`s that consume it.

**Status:** done

- [x] The outbox packages are referenced by the projects that need them, with the source generator
      referenced from the host project, which is where the library requires it
- [x] A new SharedKernel-level project owns the outbox context and its migration, leaving
      `SharedKernel.Infrastructure`'s "EF conventions only, never a concrete `DbContext`" charter intact
- [x] The connection string declares the outbox schema on its search path, with a comment saying why
- [x] `bash update-database.sh` creates the outbox table
- [x] `bash add-migration.sh` can generate a migration for the outbox context
- [x] The release workflow emits an idempotent script for the shared schema alongside the Feature ones
- [x] CI's pending-model-changes check covers the outbox context and passes
- [x] `dotnet build -warnaserror` is clean and `dotnet test` passes

## Comments

**Implemented.** `src/SharedKernel/ModulithTemplate.SharedKernel.Outbox/` owns `OutboxContext`
(schema `shared`, `IOutboxDbContext`) and the `InitialOutbox` migration; `AddOutboxDbContext` is
called from `Program.cs`, which is what puts the context in front of the tooling that enumerates the
host container. Verified against a real Postgres 18.3 container: `update-database.sh` discovers both
contexts and creates `shared.outbox`, and the library's claim SQL (`FROM outbox`, unqualified)
resolves with `Search Path=shared` and fails with `relation "outbox" does not exist` without it.
The release workflow's idempotent script was generated and applied twice to a fresh database.

Three notes for the tickets that follow.

**The release workflow needed no change.** The ticket says its "per-schema script loop needs the
shared schema added", and the spec says the same. It does not: `release-migrations.yml` already
loops over contexts discovered from the host's DI container, exactly as `update-database.sh` and the
CI check do, so registering `OutboxContext` was sufficient and it emits `OutboxContext.sql`
unprompted. No workflow file was touched.

**Snake case is not load-bearing for the outbox.** ADR 0001 and the spec imply the claim SQL's
snake-case column names depend on the solution's naming convention. They do not — the library names
every message column explicitly through an `EntityTypeConfigurationAttribute` on `OutboxMessage`,
verified by building the model with and without `UseSnakeCaseNamingConvention()` and diffing. The
convention only changes index names (`ix_` vs `IX_`). It is kept for consistency with the rest of
the DDL, and the comment on `AddOutboxDbContext` says so accurately. The *schema*, not the column
naming, is what the search path arrangement rests on, and that is what the new test pins.

**Pre-existing, unrelated: `OrdersContext` has no migration.**
`src/Features/Orders/.../Data/Migrations/` holds only a `.gitkeep`, so
`dotnet ef migrations has-pending-model-changes --context OrdersContext` exits non-zero and the
shipped CI check's `exit $status` would fail in a freshly generated project until its owner creates
the first Orders migration. Untouched here — it predates this work and is not the outbox's to fix —
but it is worth its own ticket, since it means the generated project's CI is red on first push.

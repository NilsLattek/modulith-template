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

**Status:** ready-for-agent

- [ ] The outbox packages are referenced by the projects that need them, with the source generator
      referenced from the host project, which is where the library requires it
- [ ] A new SharedKernel-level project owns the outbox context and its migration, leaving
      `SharedKernel.Infrastructure`'s "EF conventions only, never a concrete `DbContext`" charter intact
- [ ] The connection string declares the outbox schema on its search path, with a comment saying why
- [ ] `bash update-database.sh` creates the outbox table
- [ ] `bash add-migration.sh` can generate a migration for the outbox context
- [ ] The release workflow emits an idempotent script for the shared schema alongside the Feature ones
- [ ] CI's pending-model-changes check covers the outbox context and passes
- [ ] `dotnet build -warnaserror` is clean and `dotnet test` passes

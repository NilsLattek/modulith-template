# Optimistic concurrency is opt-in, per row, on Postgres's xmin, and checked only by the database

Most entities are fine with last-write-wins, so the template ships no version on any entity. An
entity opts in by implementing `IVersioned`; `ApplySharedModel()` maps its `Version` to Postgres's
`xmin` system column, so opting in needs no column and no migration. A command that changes or
removes such an item carries the version its caller read, the handler hands it to
`IRepository.ExpectVersion`, and the `UPDATE`/`DELETE` matches no row if it is stale.
`FeatureRepository<T>` turns that into a `ConcurrencyException`, and `ExceptionBehaviour` into a
`ConcurrencyError` — kept apart from a `BusinessError` because the user reloads rather than reads a
rule's message.

We rejected a version per aggregate. With `xmin` it only holds if every change to a child also
writes the root's row, and it makes every edit of one aggregate conflict with every other — which
collaborative editing cannot live with. The version is checked per row; an aggregate whose rule spans
its children versions the root and has each change write to it. We also rejected a version rule in
the entities: the database is the one check, so a stale copy cannot pass the entity and lose the
race at the save anyway.

Ported from a generated project (user-story-map) that needed it.

## Consequences

- Ties the template to Postgres; another provider needs its own row-version mapping in
  `ApplySharedModel()`.
- Saving an unchanged value sends no statement, so it succeeds whatever version it carries.
- Ardalis' `UpdateAsync`/`DeleteAsync` save at once, so `ExpectVersion` must be called before them.
- Removing an item checks only that item: children someone else just added under it go with it.

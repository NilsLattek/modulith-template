# Integration events travel through one shared outbox, written by the publishing feature's own context

Features must not share tables, but an integration event has to commit *atomically* with the domain
change that caused it, and an outbox row committed in its own transaction is a dual write with all
the failure modes the outbox exists to remove. So the outbox is the one deliberate exception to
per-feature isolation: a single `shared.outbox` table, whose row is staged by the publishing
feature's own `DbContext` and written by the same `SaveChanges` that persists the aggregate.

## Considered options

**Per-feature outbox tables.** The natural fit for the architecture, and unavailable:
`Underground.Outbox` registers `IOutboxDbContext`, `ServiceConfiguration<OutboxMessage>` and
`ClaimHeadMessage<OutboxMessage>` against the non-generic `OutboxMessage`, so a second
`AddOutboxServices` call overwrites the first. Its claim SQL also names tables unqualified, resolved
through one `search_path`, which cannot disambiguate an `outbox` table in every feature schema.

**A shared outbox context enlisted onto the feature's transaction.** Correct, and it costs a shared
`DbConnection` plus `UseTransaction` plumbing in `UnitOfWorkBase` that every feature could break
without noticing.

**A shared outbox context owning its own transaction.** Rejected outright: a dual write.

## Consequences

Every feature `DbContext` implements `IOutboxDbContext` and maps `OutboxMessage` to
`shared.outbox` with `ExcludeFromMigrations()`; only `SharedKernel.Outbox`'s `OutboxContext` owns
the DDL, and `Search Path=shared` on the connection is what lets the library's unqualified SQL find
it. A feature that maps the table anywhere else compiles and starts, then fails at its first claim —
which is why the sub-template scaffolds the mapping rather than documenting it.

`IOutbox.StageMessage` neither saves nor requires a transaction, which is what allows the row to
ride the feature's existing `SaveChanges`; it also means no push-based processing is triggered, so
the save path calls `ProcessMessages()` explicitly rather than waiting out the polling interval.

The event's full type name is a wire contract. Renaming or moving an integration event orphans the
rows already stored under the old name, and they stall their Group.

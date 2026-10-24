# UnitOfWorkBase joins an open transaction and runs under the execution strategy

Status: ready-for-agent

## What

`UnitOfWorkBase.ExecuteInTransactionAsync` always calls `BeginTransactionAsync`. That breaks twice:

- Under a retrying execution strategy (`EnableRetryOnFailure`, or Aspire's `EnrichNpgsqlDbContext`),
  EF refuses a transaction it did not start. Latent today: the template configures no retries.
- On a context already enlisted in a transaction (issue 02's inbox Consumers) it throws, because
  the connection is already in one.

Mirror `Underground.Outbox`'s `DbContextExtensions.ExecuteInTransactionAsync`: when a transaction is
open, run the action in it and leave commit and retry to its owner; otherwise run begin, action and
commit inside `Database.CreateExecutionStrategy().ExecuteAsync(...)`, clearing the change tracker on
a replay. Keep the explicit rollback and the `isolationLevel` overload.

Independent of the inbox work; ship it first.

## Acceptance

- Inside an open transaction the action runs in it, and nothing is committed or rolled back by
  `UnitOfWorkBase`.
- With `EnableRetryOnFailure` and one injected transient failure, the action is replayed and its
  writes land once.
- XML docs say the action must be re-runnable.

## Comments

**Explicit rollback dropped, against "Keep the explicit rollback".** Under a retrying strategy, a
killed backend (`pg_terminate_backend`, 57P01) made `RollbackAsync` throw `ObjectDisposedException`,
which replaced the transient error, so the strategy gave up. Disposal rolls back before the exception
leaves the attempt, ignores cancellation, and does not throw on a broken connection, as the outbox
library's version relies on. `UnitOfWorkTests.Under_a_retrying_strategy_a_broken_connection_replays_the_action`
covers it.

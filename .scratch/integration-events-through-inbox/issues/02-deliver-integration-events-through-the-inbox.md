# Deliver integration events through the shared inbox

Status: ready-for-agent
Blocked by: 01, and `Underground.Outbox` 0.22.0 (inbox Recipients, outbox repo `.scratch/inbox-recipients/issues/01`)

## What

Implement ADR 0006: the Publisher stages its integration event into `shared.inbox` in the same
`SaveChanges` as its aggregate; the library writes one row per Consumer handler; each Consumer
applies it exactly once in its own context, enlisted onto the inbox's transaction.

## Changes

- **Package.** Bump `Underground.Outbox` and `.SourceGenerator` to 0.22.0.
- **Shared inbox.** `InboxContext` in `SharedKernel.Outbox`, owning the `shared.inbox` DDL and its
  migration; `AddInboxServices<InboxContext>` in `Program.cs`. Every feature context implements
  `IInboxDbContext` and maps `InboxMessage` with `ExcludeFromMigrations()` via `ApplySharedModel()`.
  `add-migration.sh`/`update-database.sh` and CI's migration checks cover it like the outbox.
- **Publishing.** `IntegrationEventPublisher<TContext>` stages an `InboxMessage` through the
  feature's context and calls `inbox.ProcessMessages()` on the save path, replacing the outbox
  staging.
- **Consuming.** `IIntegrationEventHandler<TEvent>` in `SharedKernel.Application`
  (`Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)`).
  `IntegrationEventInboxTransport<TEvent, TContext, THandler>` in `SharedKernel.Infrastructure`
  implements `IInboxMessageHandler<TEvent>`; per call it opens a child scope, resolves `TContext`,
  points it at the inbox's connection (`SetDbConnection`, not owned) and transaction
  (`UseTransaction`), creates `THandler` with `ActivatorUtilities.CreateInstance`, runs it, saves,
  and disposes the scope.
- **Sample.** Payments' `SomeEntityAddedIntegrationEventHandler` becomes an
  `IIntegrationEventHandler<SomeEntityAddedIntegrationEvent>` without the "check first" guard;
  `Payments.Infrastructure/InboxHandlers/SomeEntityAddedInboxHandler` is its body-less transport.
- **Retire.** `IntegrationEventMediatorTransport`, Orders' `OutboxHandlers/`, and
  `IIntegrationEvent`'s mediator notification base. The outbox stays registered for effects that
  leave the database.
- **Architecture test.** Every `IIntegrationEvent` in a `Contracts` project has at least one
  `IntegrationEventInboxTransport` subclass (replaces `MSG0005`).
- **Shipped docs.** Generated `CLAUDE.md` tripwire: "Integration event handlers run inside the inbox
  transaction: keep them short and touch only your own feature's data. To reach outside the
  database, stage an outbox message." Update the `reaching-another-feature` skill, the
  `modulith-feature` sub-template's comments and `Configure<Name>Infrastructure` hints, and
  `README.md`. Grep `working/content/` for `ADR`, `docs/` and `.scratch` afterwards.
- **ADRs.** Mark 0001 and 0002 superseded by 0006 (done with the ADR).

## Acceptance

`ModulithTemplate.TemplateTests`, with a fixture using `EnableRetryOnFailure` and an interceptor
injecting one transient Npgsql failure mid-attempt:

- After a replay, the Consumer's row exists exactly once and the message is Completed.
- A Consumer that throws leaves no rows of its own and its message is not Completed.
- With two Consumers of one event, one failing leaves the other's row Completed and its data
  committed.
- The Publisher's aggregate and its inbox rows commit in one `SaveChanges`, or neither does.

Plus: `-warnaserror` build clean; scaffold a solution and a feature into it, build, and confirm no
`ModulithTemplate`, `FeatureName` or `ModulithApp` survives.

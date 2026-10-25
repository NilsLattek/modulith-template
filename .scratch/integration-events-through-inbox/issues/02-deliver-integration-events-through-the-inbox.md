# Deliver integration events through the shared inbox, one row per Consumer

Status: ready-for-agent
Blocked by: 01

## What

Implement ADR 0006 on `Underground.Outbox` 0.21.1: the Publisher's save stages one `shared.inbox`
row per Consumer, each an `InboxEnvelope<TConsumerContext, TEvent>`, and each Consumer applies it
exactly once by sending a Command in its own context, enlisted onto the inbox's transaction.

**Prove first:** the source generator registers a body-less class deriving from
`IntegrationEventInboxTransport<TEvent, TContext>`, whose base implements
`IInboxMessageHandler<InboxEnvelope<TContext, TEvent>>`, with `MessageTypeName` equal to
`typeof(InboxEnvelope<TContext, TEvent>).FullName`. If it does not, stop and report.

## Changes

- **Shared inbox.** `InboxContext` in `SharedKernel.Outbox`, owning the `shared.inbox` DDL and its
  migration; `AddInboxServices<InboxContext>` in `Program.cs`. `add-migration.sh`,
  `update-database.sh` and CI's migration checks cover it like `OutboxContext`.
- **Envelope and routes.** `InboxEnvelope<TContext, TEvent>(TEvent Event)`. `AddModuleDbContext<TContext>`
  also registers an `InboxRoute<TContext>` singleton with `Wrap<TEvent>(TEvent)`; no reflection.
- **Publishing.** `IIntegrationEventPublisher.Publish<TEvent>(TEvent) where TEvent : IIntegrationEvent`.
  `IntegrationEventPublisher<TContext>` builds, once, the set of registered
  `HandlerEntry<InboxMessage>.MessageTypeName`s and `HandlerEntry<OutboxMessage>` message types. Per
  event it stages, through the Publisher's own context:
  - one `InboxMessage` per route whose envelope type name is in that set, with EventId = UUIDv5 of
    (envelope type name, event `EventId`) and GroupKey = Consumer context name + event `GroupKey`;
  - one `OutboxMessage` of the bare event when an outbox handler for `TEvent` is registered;
  - and throws when neither applies.
  Feature contexts implement `IInboxDbContext` and map `InboxMessage` with `ExcludeFromMigrations()`
  in `ApplySharedModel()`, beside the outbox mapping.
- **Consuming.** `IntegrationEventInboxTransport<TEvent, TContext>` in `SharedKernel.Infrastructure`,
  with `protected abstract ICommand<Result> ToCommand(TEvent integrationEvent)`. Per message: open a
  child scope, resolve `TContext`, `SetDbConnection` (not owned) and `UseTransaction` onto the
  inbox's, send `ToCommand(...)` through that scope's `ISender`, and throw on a failed `Result`.
- **Version pin.** `<AssemblyVersion>1.0.0.0</AssemblyVersion>` in `Directory.Build.props`, with a
  one-line comment saying the envelope's wire name embeds it.
- **Sample.** Payments' `SomeEntityAddedIntegrationEventHandler` becomes `RecordPaymentCommand` and
  its handler, without the "check first" guard; `Payments.Infrastructure/InboxHandlers/SomeEntityAddedInboxHandler`
  maps the event to it.
- **Retire.** `IntegrationEventMediatorTransport`, Orders' `OutboxHandlers/`, and `IIntegrationEvent`'s
  `INotification` base. The outbox stays registered for effects that leave the process.
- **Architecture tests.** Every `IIntegrationEvent` in a Contracts project has an
  `IntegrationEventInboxTransport` subclass or an `IOutboxMessageHandler<T>` (replaces `MSG0005`).
  No project overrides `AssemblyVersion`. Every feature context maps `InboxMessage`, like
  `OutboxMappingTests`.
- **Sub-template.** `modulith-feature` scaffolds the `InboxMessage` mapping and an `InboxHandlers/`
  hint; `Configure<Name>Infrastructure`'s comment says: to send `T` outside the process, declare an
  `IOutboxMessageHandler<T>`.
- **Shipped docs.** Generated `CLAUDE.md` tripwire: "Inbox handlers run inside the inbox transaction:
  the Command they send must stay short and touch only this feature's data; reach outside the
  database by staging an outbox message." Update `reaching-another-feature`, the
  `IIntegrationEvent` remarks (exactly once per Consumer) and `README.md`. Grep `working/content/`
  for `ADR`, `docs/` and `.scratch` afterwards.

## Acceptance

`ModulithTemplate.TemplateTests`, with a fixture using `EnableRetryOnFailure` and an interceptor
injecting one transient Npgsql failure mid-attempt:

- The Publisher's aggregate and its inbox rows commit in one `SaveChanges`, or neither does.
- With two Consumers of one event, one whose Command fails leaves the other's data committed and its
  row Completed, and its own row not Completed with none of its writes.
- After a replay, the Consumer's data exists exactly once and its row is Completed.
- A feature context with no handler for the event gets no row.
- Publishing an event with no inbox transport and no outbox handler throws.

Plus: `-warnaserror` build clean; scaffold a solution and a feature into it, build, and confirm no
`ModulithTemplate`, `FeatureName` or `ModulithApp` survives.

## Comments

**Prove-first held.** The generator registers `SomeEntityAddedInboxHandler` under
`typeof(InboxEnvelope<PaymentsContext, SomeEntityAddedIntegrationEvent>).FullName`;
`IntegrationEventDeliveryTests` pins it through the real composition.

**Choices the ticket left open.**
- The publisher reads the generator's registrations through an `IntegrationEventSubscriptions`
  singleton (registered by `AddModuleDbContext`), and decides inbox and outbox by `typeof(TEvent)`.
- The transport takes the processing scope's `IServiceProvider`, so a Consumer's subclass has one
  constructor argument.
- Inbox migrations land in `SharedKernel.Outbox/Data/InboxMigrations`, history table
  `shared.__EFMigrationsHistory_Inbox`; `add-migration.sh Inbox <Name>`.
- `ContractIsolationTests` now lets `Infrastructure.InboxHandlers` (and the generator's registration
  class) name a sibling's Contracts; the rest of Infrastructure still may not.
- `RecordPaymentCommand` has a validator: the generated `CLAUDE.md` asks for one on a command sent by
  a consumer. `PaymentForOrderSpec` lost its only caller and was removed.
- `SolutionUnderTest.DeliverAsync` reaches the library's internal `IProcessor<InboxMessage>` by name,
  and waits for rows held back by `pg_snapshot_xmin` (parallel tests delay stability).

**Found, not fixed (pre-existing, also on `main`).** In the running host, Orders' domain event is never
dispatched, so no integration event is published at all. `Orders.Domain` is referenced with
`PrivateAssets="all"`, so the host's mediator source generator never sees
`SomeEntityAddedDomainEvent`, and `Publish(object)` drops it silently. TemplateTests reference
`Orders.Domain` directly, which masks it. Seen by driving the Orders form in a scaffolded app: the entity
is saved, no inbox row is staged.

**Revised after review: fan-out moved from publish time to the outbox handler.** The publish-time
`IntegrationEventSubscriptions` / `InboxRoute` design was hard to read and decided two things at once
(who consumes, whether the event leaves the process). Now:
- The Publisher stages one outbox row, as before this ticket. Feature contexts no longer map the inbox.
- `IntegrationEventOutboxHandler<TEvent>` (was `IntegrationEventMediatorTransport`) only publishes
  through the mediator.
- `IntegrationEventInboxHandler<TEvent, TContext>` (was `IntegrationEventInboxTransport`) is also the
  notification handler that adds its own row via `IInbox.AddMessageAsync` in its own transaction,
  treating a unique violation on `shared.inbox` as already delivered. Dependencies are injected, not
  resolved from `IServiceProvider`.
- Forwarders are plain `INotificationHandler<TEvent>`s in Infrastructure; `IIntegrationEvent` is an
  `INotification` again, so `MSG0005` is back. Architecture tests: an outbox handler per event; plain
  notification handlers of Integration Events only in Infrastructure.
- TemplateTests always compose the Auditing Consumer, because its notification handler is in that
  assembly's `AddMediator` regardless; `DeliverAsync` drains the outbox, then the inbox.

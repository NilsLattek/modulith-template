# Integration events fan out into the shared inbox at publish time, one row per Consumer

ADR 0002 delivered one outbox message to every Consumer through the mediator, so a Consumer that threw
re-ran its siblings. Each Consumer should instead apply an event exactly once, in a transaction of its
own. `Underground.Outbox` 0.21.1 has one inbox per application and dispatches each stored type name to
exactly one handler, so the Publisher's `SaveChanges` stages one `shared.inbox` row per Consumer, each
wrapping the event in an `InboxEnvelope<TConsumerContext, TEvent>` whose type name is distinct per
Consumer. The Publisher names no Consumer: it stages a row for every feature context whose envelope
for that event has a registered handler, read from the handlers the source generator registered.

## Considered options

**Library-side fan-out** (Recipients in 0.22.0, or named Inboxes). Cleaner, and unreleased. It can
replace this later without changing how a Consumer is written.

**A mediator in front of the inbox** to choose between in-process and external delivery. It runs in
memory inside the Publisher's save, so a handler there is a dual write. The choice is made instead by
declaring a handler: an inbox transport per in-process Consumer, an `IOutboxMessageHandler<TEvent>`
for anything leaving the process. Neither declared throws at publish time.

**A per-feature envelope class** (`PaymentsInboxEnvelope<T>`), built by reflection. The shared
envelope is keyed by the Consumer's context, which the transport already names.

**A random or UUIDv7 row EventId.** The row's EventId is UUIDv5 of the envelope type name and the
event's `EventId`, so a replayed save stages the same ids. The library orders a Group by
transaction and append order, never by EventId.

## Consequences

**The Consumer's context is enlisted onto the inbox's connection and transaction.** The transport
opens a child scope per message, points the feature context at the inbox's connection and
transaction, and sends a Command built from the event; `UnitOfWorkBase` joins that transaction.
Without the enlistment the Consumer's writes would commit apart from the row's completion.

**The envelope's type name is a wire contract, and it embeds assembly versions.** A closed generic's
`FullName` carries `Version=` for each type argument, so `AssemblyVersion` is pinned in
`Directory.Build.props` and an architecture test fails on any override. Renaming the event, its
Contracts project or the Consumer's context still orphans pending rows.

**A refused Command fails the message.** It retries with backoff, stalling only that Consumer's Group
for that aggregate, because the row's GroupKey is prefixed with the Consumer. A Consumer that accepts
a refusal models it in its Command.

**One reaction per feature per event**, since one envelope type has one handler.

**The Consumer set is fixed when an event is recorded.** A Consumer added later never sees earlier
events; one that drops its handler leaves rows that stall its Groups, so it drains them first.

**Consumers run inside a write transaction**, which holds back `pg_snapshot_xmin` while they run, so
they stay short and touch only their own feature's data.

`IIntegrationEvent` is no longer a mediator notification, so `MSG0005` no longer covers it; the
publish-time throw and an architecture test requiring an inbox transport or outbox handler per
event replace it.

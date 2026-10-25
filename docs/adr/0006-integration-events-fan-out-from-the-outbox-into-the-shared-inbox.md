# Integration events fan out from the outbox into the shared inbox, one row per Consumer

ADR 0002 delivered one outbox message to every Consumer through the mediator, so a Consumer that threw
re-ran its siblings. Each Consumer should instead apply an event exactly once, in a transaction of its
own. `Underground.Outbox` 0.21.1 has one inbox per application and dispatches each stored type name to
exactly one handler, so delivery takes two hops:

1. The Publisher's save stages **one outbox row**, as before.
2. The publishing feature's `IntegrationEventOutboxHandler<TEvent>` publishes the claimed event through
   the mediator. Each Consumer's `IntegrationEventInboxHandler<TEvent, TContext>` is also its
   notification handler, and stages one `shared.inbox` row wrapping the event in an
   `InboxEnvelope<TConsumerContext, TEvent>`, whose type name is distinct per Consumer. The outbox
   handler saves all the rows in one `SaveChanges`.
3. The inbox worker hands each row to the same `IntegrationEventInboxHandler`, which sends the
   Consumer's Command inside the inbox's transaction.

The mediator's generator is the Consumer registry: the Publisher names no Consumer, and nothing reads
the outbox library's handler registrations.

## Considered options

**Fan-out at publish time** (this ADR's first version): the Publisher's save staged one inbox row per
Consumer, found by matching the outbox generator's registered handler names against a route per feature
context, plus an outbox row when a forwarder was registered. One hop, but the Publisher decided both
who consumes and whether the event leaves the process, through type-name introspection that was hard
to read.

**Library-side fan-out** (Recipients in 0.22.0, or named Inboxes). Cleaner, and unreleased. It can
replace the middle hop later without changing how a Consumer is written.

**Querying the inbox for existing rows before staging**, to make a redelivered fan-out idempotent.
Catching the unique violation does the same in one place, because the rows are saved all-or-nothing.

## Consequences

**A redelivered fan-out succeeds without writing.** The outbox delivers at least once with no
transaction open, so a lost completion offers the event again. Row ids are UUIDv5 of the envelope type
name and the event's `EventId`, so the save hits the inbox's unique index; the outbox handler treats
that violation on `shared.inbox` as already delivered. A Consumer added between the two attempts gets
no row for that event.

**Forwarding outside the process is a plain `INotificationHandler<TEvent>`**, in `Infrastructure`. It
runs during fan-out, at least once. If it throws, no Consumer's row is saved and the outbox row is
retried, so a down broker delays the in-process Consumers of that event too.

**Two polls per event** instead of one, and an outbox row as well as the inbox rows.

**The Consumer's context is enlisted onto the inbox's connection and transaction.** Its writes and
the row's completion commit together; `UnitOfWorkBase` joins that transaction.

**The envelope's type name is a wire contract, and it embeds assembly versions.** A closed generic's
`FullName` carries `Version=` for each type argument, so `AssemblyVersion` is pinned in
`Directory.Build.props` and an architecture test fails on any override. Renaming the event, its
Contracts project or the Consumer's context still orphans pending rows.

**A refused Command fails the message.** It retries with backoff, stalling only that Consumer's Group
for that aggregate, because the row's GroupKey is prefixed with the Consumer.

**One reaction per feature per event**, since one envelope type has one handler.

**The Consumer set is fixed at fan-out.** A Consumer added later never sees events already fanned out;
one that drops its handler leaves rows that stall its Groups, so it drains them first.

**Consumers run inside a write transaction**, which holds back `pg_snapshot_xmin` while they run, so
they stay short and touch only their own feature's data.

`IIntegrationEvent` stays a mediator notification, so `MSG0005` fails the build for an event nobody
handles; an architecture test requires an outbox handler per event, and another keeps plain
notification handlers of Integration Events in `Infrastructure`, where only forwarders belong.

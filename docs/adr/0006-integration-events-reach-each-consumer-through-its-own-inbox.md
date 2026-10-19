---
status: proposed
---

# Integration events reach each Consumer through its own Inbox, fanned out in the Publisher's save

ADR 0002 delivered one outbox message to every Consumer through the mediator, so a Consumer that threw
re-ran its siblings and every Consumer had to be idempotent against a sibling's failure. Each Consumer
should instead receive an event exactly once, in a transaction of its own, with no effect on any other.

So every Consumer Feature has a named Inbox in the shared `shared.inbox` table (library ADR 0012), and
the Publisher's `SaveChanges` stages one inbox row per Consumer beside the aggregate. The Publisher
still names no Consumer: the library broadcasts to every Inbox whose Handlers handle the type. Each
Consumer's processor runs on that Feature's own context, so its writes and the row's completion commit
together. Groups are scoped to the Consumer, so a poison message stalls only that Consumer's Group.

A Consumer implements `IIntegrationEventHandler<TEvent>` in its Application layer; Infrastructure
declares a body-less adapter the library discovers. `IIntegrationEvent` is no longer an
`INotification`, and the mediator path is gone.

## Considered options

**Fan out at delivery**: the outbox Handler writes the inbox rows. Adds a hop that is at-least-once,
and so has to tolerate duplicate-key failures on redelivery, for nothing the publish-time fan-out lacks.

**Enlist each Consumer's context onto one shared inbox transaction.** The plumbing ADR 0001 rejected,
plus derived EventIds and per-Consumer wrapper types to get past the library's identity rules.

## Consequences

The template's outbox carries only effects outside the database; the per-event mediator transports go.

An Integration Event handler writes only to its own Feature's database and stays short: it runs in a
write transaction that holds back delivery for every Group, and exactly-once covers nothing outside it.

The Consumer set is fixed when an event is recorded. A Consumer added later never sees earlier events;
a Consumer dropping its Handler leaves rows that stall its Group, so it drains first.

`MSG0005` no longer covers Integration Events. An architecture test requires a handler for every
`IIntegrationEvent`, and staging a type no Inbox handles throws.

Supersedes the Consequences of ADR 0002, and narrows ADR 0001's outbox to external effects, once
implemented.

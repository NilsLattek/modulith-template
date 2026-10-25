---
name: reaching-another-feature
description: Get data from another feature or make something happen in one, using a Module API or an Integration Event. Use when a feature needs data it does not own, when something in one feature should trigger work in another, or when publishing or consuming an integration event — and whenever you are tempted to add a project reference between two features.
---

# Reaching another feature

Features share no types, tables or transactions. A project reference from one feature to another is
never the answer — the architecture tests fail it. Both legitimate routes go through the publishing
feature's **`Contracts`** project, the only part of a feature a sibling may reference.

## Pick the route first

| You need… | Use | Shape |
| --- | --- | --- |
| An answer, **now**, to finish the request you are in | **Module API** | synchronous, read-only, the caller's transaction |
| Something to happen elsewhere **because** something happened here | **Integration Event** | durable, exactly once per consumer, each in its own transaction |

If you are about to call a Module API and ignore its return value, you wanted an event. If you are
about to publish an event and then wait for its result, you wanted a Module API.

## Module API

The quick one. Declare the interface in the owning feature's `Contracts/Api/` (`IOrdersApi`),
implement it in that feature's `Application/Api/`. The consumer injects the interface. It is
**read-only** — a Module API never mutates the owning feature's state; that is what an event is for.

## Integration Event

Modelled on the shipped `Orders` → `Payments` example.

### Publishing

1. Declare `SomeEntityAddedIntegrationEvent : IIntegrationEvent` in `Orders.Contracts/Events/`.
2. Raise a domain event from the aggregate's mutator, and translate it in a handler under
   `Orders.Application/DomainEventHandlers/` that calls `IOrdersIntegrationEventPublisher.Publish(...)`.
   The event is recorded by the same save as the aggregate's change.
3. Declare a body-less `SomeEntityAddedOutboxHandler : IntegrationEventOutboxHandler<TEvent>` in
   `Orders.Infrastructure/OutboxHandlers/`.

**Set `GroupKey` to the aggregate the event concerns.** Events sharing one reach each consumer in
order, one at a time, while other aggregates proceed concurrently; a constant would serialise the
whole application behind one stuck message.

### Consuming

1. Write the reaction as an ordinary Command of the consuming feature (`RecordPaymentCommand` and its
   handler in `Payments.Application/Commands/RecordPayment/`).
2. Declare `SomeEntityAddedInboxHandler : IntegrationEventInboxHandler<TEvent, PaymentsContext>` in
   `Payments.Infrastructure/InboxHandlers/`, overriding `ToCommand` to map the event to that Command.

Both handler kinds are registered by the source-generated `Add<Assembly>MessageHandlers()`, called once
in the feature's `Configure<Name>Infrastructure`; the publisher never names a consumer.

Each consumer applies each event **exactly once, in a transaction of its own**, so the Command opens no
transaction and needs no "already done?" check.

- **A failed `Result` is retried** with backoff, a Business Rule Violation included. It stalls only that
  consumer's Group for that aggregate. A consumer that should accept a refusal models it in its Command.
- **One inbox handler per feature per event.**
- **Keep the Command short and inside this feature's data**: it holds the inbox transaction open. Reach
  outside the database (email, HTTP) by staging an outbox message.
- **React only through an inbox handler.** A plain `INotificationHandler` runs at least once, outside
  the consumer's transaction, and is for forwarding (below).

### Sending an event outside the process

Declare a plain `INotificationHandler<TEvent>` in an `Infrastructure` project that sends it to the
broker or other system. It runs **at least once**, so the receiver deduplicates on `EventId`. When it
throws, the event is retried, which can delay the in-process consumers of that event too.

### Things that bite

- **The inbox row's type name is a wire contract.** It names the event, its `Contracts` project and
  the consumer's `DbContext`, so renaming any of them orphans pending rows, which stall their Group:
  drain before renaming. `AssemblyVersion` is pinned in `Directory.Build.props` for the same reason;
  version releases through `Version`.
- **The consumer set is fixed at delivery.** A consumer added later never sees events already
  delivered; one removing its inbox handler lets its pending rows drain first.
- **An event with no consumer is a defect.** Either the consumer is missing or the event should not
  exist; the build fails one that nothing handles.

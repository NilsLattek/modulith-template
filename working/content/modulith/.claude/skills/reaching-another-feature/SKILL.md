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

### Where each piece lives

Modelled on the shipped `Orders` → `Payments` example:

| Piece | Lives in |
| --- | --- |
| `SomeEntityAddedDomainEvent`, raised by the aggregate's mutator | `Orders.Domain/Events/` |
| `SomeEntityAddedIntegrationEvent : IIntegrationEvent` — the published contract | `Orders.Contracts/Events/` |
| The handler translating the one into the other | `Orders.Application/DomainEventHandlers/` |
| `SomeEntityAddedOutboxHandler : IntegrationEventOutboxHandler<TEvent>` — body-less, one per published event | `Orders.Infrastructure/OutboxHandlers/` |
| `RecordPaymentCommand` and its handler — the consumer's reaction, an ordinary Command | `Payments.Application/Commands/RecordPayment/` |
| `SomeEntityAddedInboxHandler : IntegrationEventInboxHandler<TEvent, PaymentsContext>` — maps the event to that Command | `Payments.Infrastructure/InboxHandlers/` |

The publisher binding (`OrdersIntegrationEventPublisher` in `Orders.Infrastructure/Events/`) is
already scaffolded. Each feature's `Configure<Name>Infrastructure` calls the source-generated
`Add<Assembly>MessageHandlers()`, which registers every outbox and inbox handler in that assembly —
so consuming another event costs a Command and a one-method inbox handler, with **no registration to
keep in step**. The publisher never names a consumer, so adding one never changes the publishing feature.

### How delivery works

Two hops, so each consumer gets the event exactly once without the publisher knowing who consumes it:

1. **Outbox.** The translating handler hands the event to its feature's `I<Name>IntegrationEventPublisher`,
   which **stages** one outbox row through the publisher's own `DbContext`, so it exists if and only if
   the aggregate's change committed.
2. **Fan-out.** The outbox worker gives the row to the publisher's outbox handler, which publishes the
   event through the mediator: every consuming feature's inbox handler stages its own inbox row, and the
   rows are saved together. The outbox delivers at least once; a redelivery finds its rows already
   saved and succeeds without adding any.
3. **Inbox.** The inbox worker applies each row in a transaction of its own: the inbox handler sends its
   Command inside that transaction, so the consumer's writes and the row's completion commit together.

**No feature code opens a transaction**, and the Command needs no "already done?" check.

- **A failed `Result` is retried**, a Business Rule Violation included, with backoff. It stalls only
  that consumer's Group for that aggregate; other consumers are unaffected. A consumer that should
  accept a refusal models it in its Command.
- **One reaction per feature per event**: an event gets one inbox handler per consuming feature.
- **Keep the Command short and inside this feature's data** — it holds the inbox transaction open. An
  effect outside the database (email, HTTP) is staged as an outbox message instead.
- **Never a plain `INotificationHandler` to react** to an Integration Event: it runs during fan-out,
  at least once, outside any transaction of the consumer's. It is only for forwarding (below), and the
  architecture tests fail one outside `Infrastructure`.

The inbox handler is the one place an `Infrastructure` layer may name a sibling's `Contracts` type;
`ContractIsolationTests` fails it anywhere else in that layer.

### Sending an event outside the process

To forward an event to a broker or another system, declare a plain `INotificationHandler<TEvent>` in an
`Infrastructure` project that sends it. It runs during fan-out beside the consumers' staging, so it is
delivered **at least once**, and the receiving system deduplicates on `EventId`. If it throws, no
consumer's row is saved and the whole outbox row is retried, so a down broker delays the in-process
consumers of that event too.

An event without an outbox handler fails the architecture tests; one with no handler at all fails the
build (`MSG0005`).

### Group keys

`EventId` and `GroupKey` live on the event itself, because delivery hands a consumer the event and
nothing else. **The Group key is the aggregate the event concerns**; each inbox row prefixes it with its
consumer, so one consumer's stuck row holds back no other. Events sharing one are delivered in
order, one at a time, while unrelated aggregates proceed concurrently — a constant would serialise
the whole application behind a single stuck message.

### Things that bite

- **The inbox row's type name is a wire contract.** It names the event, its `Contracts` project and
  the consumer's `DbContext`, so renaming any of them orphans pending rows, which stall their Group:
  drain before renaming. `AssemblyVersion` is pinned in `Directory.Build.props` for the same reason;
  version releases through `Version`.
- **The consumer set is fixed at fan-out.** A consumer added later never sees events already fanned
  out; one removing its inbox handler must let its pending rows drain first.
- **An event with no consumer is a defect, not a resting state.** If nothing reacts to it, either
  the consumer is missing or the event should not exist.

The architecture tests fail an `IIntegrationEvent` declared outside a `Contracts` assembly, or named
or placed off-convention.

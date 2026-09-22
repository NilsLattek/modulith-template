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
| Something to happen elsewhere **because** something happened here | **Integration Event** | durable, at least once, each consumer in its own transaction |

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
| `SomeEntityAddedOutboxHandler : IOutboxMessageHandler<T>` — takes the delivered row to the mediator | `Orders.Infrastructure/OutboxHandlers/` |
| `SomeEntityAddedIntegrationEventHandler : INotificationHandler<T>` — the consumer | `Payments.Application/IntegrationEventHandlers/` |

The publisher binding (`OrdersIntegrationEventPublisher` in `Orders.Infrastructure/Events/`) and its
registration are already scaffolded. `Configure<Name>Infrastructure` calls the source-generated
`Add<Assembly>MessageHandlers()`, which picks up every handler under `OutboxHandlers/` — so a second
published event costs a record, a translating handler and an outbox handler, with **no registration
to keep in step**.

### How delivery works

The translating handler hands the event to its feature's `I<Name>IntegrationEventPublisher`, which
**stages** an outbox row through that feature's own `DbContext`. The row is written by the same save
that persists the aggregate, so the event exists if and only if the change committed. A worker later
claims the row and hands it, in a fresh scope, to the **one** `IOutboxMessageHandler<T>` registered
for its type, which publishes it to the mediator — where every consumer receives it in that scope
with its own `DbContext`. **No feature code opens a transaction.**

The outbox handler belongs to the feature whose `Contracts` declares the event, in its
`Infrastructure` layer: delivery is infrastructure, and it is the reading counterpart to the
publisher that staged the row. It is the one thing an `Infrastructure` layer may name a `Contracts`
type for, and only its own feature's — `ContractIsolationTests` still fails it for a sibling's.

### Group keys

`EventId` and `GroupKey` live on the event itself, because delivery hands a consumer the event and
nothing else. **The Group key is the aggregate the event concerns.** Events sharing one are
delivered in order, one at a time, while unrelated aggregates proceed concurrently — a constant
would serialise the whole application behind a single stuck message.

### Two things that bite

- **The event's full type name is a wire contract.** Renaming or moving an integration event
  orphans rows already stored under the old name, and they stall their Group.
- **An event with no consumer is a defect, not a resting state.** If nothing reacts to it, either
  the consumer is missing or the event should not exist.

The architecture tests fail an `IIntegrationEvent` declared outside a `Contracts` assembly, or named
or placed off-convention.

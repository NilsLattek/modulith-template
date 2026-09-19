# An integration event is dispatched by the feature whose Contracts declares it

A claimed outbox row carries a JSON payload and a type name; something has to turn that back into
the event and publish it. That something is now one `IOutboxMessageHandler<T>` per integration
event, living in the `Infrastructure` layer of the feature that owns the event — the same feature
that declared the record and staged the row.

`Infrastructure` because delivery is infrastructure: the handler is the reading counterpart to the
publisher that staged the row, and neither is a domain or application concern. It does have to name
a `Contracts` type, which is why `ContractIsolationTests` now admits `Infrastructure` as a consumer
— but only of **its own** feature's `Contracts`. `Application` keeps the broader licence, because
consuming a *sibling's* contract is the thing it exists to do.

## Considered options

**One central dispatcher for every event.** What this replaces: an `IntegrationEventRepublisher`
registered as `IMessageDispatcher<OutboxMessage>` *after* `AddOutboxServices` so as to displace the
library's own, resolving the row's type through an `IntegrationEventRegistry` each feature fed from
its `Configure<Name>Application`. It existed only because `Underground.Outbox` 0.16 discovered
handlers from the composition root's compilation, which no generic republisher could satisfy: an
open generic handler made the generator emit a type parameter it could not resolve, and a handler
closing one by inheritance was not discovered at all.

0.17 discovers handlers **per assembly** instead — each project declaring them emits its own
`Add<Assembly>MessageHandlers()`, which the owning module calls, and `AddOutboxServices` became an
ordinary library method. A per-event handler is then exactly what the library wants, so the central
dispatcher, the registry, and the registration-order rule in `Program.cs` all go.

**The consuming feature owning the handler.** Rejected: the outbox allows one handler per message
type, so ownership has to be unambiguous, and the publisher is the only party that is. A second
consumer would otherwise have to negotiate with the first.

**The handler in `Application`.** The first shape this took, and it reads as the smaller change,
since `Application` could already name a `Contracts` type. Rejected on layering: turning a stored
row back into an object is the outbox's business, not the application's, and putting it in
`Application` also meant `Underground.Outbox` — EF Core and Npgsql with it — in a layer that had
been free of them.

## Consequences

An event may have exactly **one** outbox handler — `OUTBOX001` within an assembly,
`CompetingHandlersException` at startup across them. Fan-out is unaffected and stays the mediator's,
as ADR 0002 describes: the handler publishes once and every `INotificationHandler` for that event
runs, so consuming still costs a consumer nothing but a handler.

`Infrastructure` gains a `ProjectReference` to its own `Contracts`, and with it a rule that has to
be enforced rather than assumed: `ContractIsolationTests` checks per feature that an
`Infrastructure` layer names no *other* feature's `Contracts`, which `FeatureModuleTests` cannot see
because it drops Contracts types out of its slices by design.

The repeated-failure warning the republisher emitted is gone with it. A permanently failing message
still stalls its Group forever, and noticing that is now the host's job through the outbox's own
logging and unhandled-row depth rather than a warning of ours.

An unclaimed type still fails loudly rather than completing silently: the library raises
`No handler configured for message type '<name>'`, which is also what a renamed or moved event
produces for the rows written under its old name.

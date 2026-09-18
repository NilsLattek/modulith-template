# An integration event is dispatched by the feature whose Contracts declares it

A claimed outbox row carries a JSON payload and a type name; something has to turn that back into
the event and publish it. That something is now one `IOutboxMessageHandler<T>` per integration
event, living in the `Application` layer of the feature that owns the event — the same feature that
declared the record and staged the row.

`Application` rather than `Web`, because the handler must name a `Contracts` type and
`ContractIsolationTests` leaves `Application` as the only layer that may. That is the one place this
mechanism's layering differs from `I<Name>IntegrationEventPublisher`, which is bound in `Web`
precisely because it must *not* name one.

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

## Consequences

An event may have exactly **one** outbox handler — `OUTBOX001` within an assembly,
`CompetingHandlersException` at startup across them. Fan-out is unaffected and stays the mediator's,
as ADR 0002 describes: the handler publishes once and every `INotificationHandler` for that event
runs, so consuming still costs a consumer nothing but a handler.

The handler interface ships in `Underground.Outbox`, which is not abstractions-only, so a feature's
`Application` layer picks up EF Core and Npgsql transitively. Accepted; the alternative is that same
package in `SharedKernel.Application`, which reaches every feature's `Contracts` project.

The repeated-failure warning the republisher emitted is gone with it. A permanently failing message
still stalls its Group forever, and noticing that is now the host's job through the outbox's own
logging and unhandled-row depth rather than a warning of ours.

An unclaimed type still fails loudly rather than completing silently: the library raises
`No handler configured for message type '<name>'`, which is also what a renamed or moved event
produces for the rows written under its old name.

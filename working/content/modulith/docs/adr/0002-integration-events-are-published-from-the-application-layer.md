# Integration events are published from the application layer, and delivered at least once

A domain event is internal and speaks the feature's own language; an integration event is a
published contract. Translating one into the other is an application concern — the domain model
must not know that siblings, or a delivery mechanism, exist — so an aggregate raises only a domain
event, and an application-layer handler turns it into the integration event.

`IIntegrationEvent` and `IIntegrationEventPublisher` therefore live in `SharedKernel.Application`,
and a feature's `I<Name>IntegrationEventPublisher` in its own `Application` layer — unlike
`IRepository` and `IUnitOfWork`, which sit in `Domain` because a repository genuinely *is* a domain
abstraction. One consequence is easy to trip over: `FeatureLayerTests` forbids `Infrastructure` from
depending on `Application`, so the publisher is bound in the feature's `Web` composition root
(`<Name>Module.cs`) rather than beside the repository in `Configure<Name>Infrastructure`.

## Consequences

Delivery is **at least once**. One outbox message fans out to every consumer through the mediator,
so a consumer that throws fails the whole message: it is redelivered, re-running consumers that had
already succeeded, and consumers after the failing one may not have run at all on the first attempt.
Handlers must be idempotent in the strong sense — tolerant of re-running after a *sibling* failed.

The obvious fix, one outbox message per consumer, is rejected: it requires the publisher to
enumerate its subscribers, which is the coupling integration events exist to remove. A consumer that
cannot be made idempotent wraps the event in a message type of its own and takes an inbox row for
it; the shared inbox cannot serve that directly, because one row per event id would let the first
consumer to complete starve the rest.

An integration event with no consumer fails the host build (`MSG0005`), and that is deliberate:
in a monolith every consumer is in the same solution, so "nobody listens to this" is a defect rather
than a deployment state.

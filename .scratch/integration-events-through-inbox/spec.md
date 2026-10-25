# Spec: Integration events fan out into the shared inbox, one row per Consumer

Status: ready-for-agent

Decision record: ADR 0006 (revised: fan-out moved from the Publisher's save to the outbox handler). Implementation ticket: `issues/02-deliver-integration-events-through-the-inbox.md`.

## Problem Statement

A generated project's Integration Events are delivered **at least once**. The Publisher's save stages
one outbox row; the outbox worker publishes it through the mediator to every Consumer at once. When one Consumer throws, the whole message is redelivered: Consumers that
already succeeded run again, and Consumers after the failing one may not have run at all. Every
Consumer therefore has to be idempotent against a *sibling's* failure, and the sample Consumer carries
a "check first" guard to survive it. A developer writing a Consumer has to reason about every other
Consumer of the same event, and one broken Consumer holds back all of them.

## Solution

Each Consumer receives each Integration Event **exactly once, in a transaction of its own**, and one
Consumer failing neither repeats nor holds back another. The Publisher's save stages one outbox row,
atomically with the aggregate, as before. The outbox handler publishes the event through the mediator,
where each Consumer stages its own row into the shared inbox, and saves them together; the Publisher
names no Consumer. Each Consumer reacts by sending one of its own Commands, which runs inside the
inbox's transaction, so the Consumer's change and the row's completion commit together. Events that
must leave the process (Kafka, email, HTTP) are forwarded by a plain notification handler, at least
once. Consumers no longer need idempotency guards.

## User Stories

1. As a Consumer's developer, I want each Integration Event applied to my feature exactly once, so that I write no idempotency guard.
2. As a Consumer's developer, I want another Consumer's failure to leave my feature's processing untouched, so that I never debug a sibling's bug as my own.
3. As a Consumer's developer, I want my reaction to be an ordinary Command of my feature, so that it gets the same validation, logging and Business Rule Violation handling as any other operation.
4. As a Consumer's developer, I want to declare a Consumer with one small class mapping the event to a Command, so that subscribing costs one line of real code.
5. As a Consumer's developer, I want no envelope or wrapper class to write per event, so that subscribing to many events adds no boilerplate.
6. As a Consumer's developer, I want my Command's writes to commit together with the inbox row's completion, so that a crash never leaves a half-applied or re-applied event.
7. As a Consumer's developer, I want a refused Command to be retried rather than dropped, so that an event arriving before its prerequisite is applied once the prerequisite lands.
8. As a Consumer's developer, I want a refused or failing event to stall only my feature's Group for that aggregate, so that the rest of my feature and every other Consumer keep flowing.
9. As a Consumer's developer, I want events in one Group to reach my feature in the order they were recorded, so that I can rely on causal order per aggregate.
10. As a Consumer's developer, I want a transient database failure during my Command to replay it cleanly with its writes landing once, so that a retrying execution strategy is safe.
11. As a Consumer's developer, I want my Command to be able to stage an outbox message, so that an effect leaving the database still happens only if my change commits.
12. As a Publisher's developer, I want publishing to stay one call that names no Consumer, so that adding a Consumer never changes my feature.
13. As a Publisher's developer, I want the outbox row to commit in the same save as my aggregate, or not at all, so that an event exists if and only if the change it describes does.
14. As a Publisher's developer, I want an event nobody handles to fail the build, so that a forgotten Consumer is found at once rather than in production data.
15. As a Publisher's developer, I want to send an event outside the process by declaring a notification handler for it, so that external delivery needs no second mechanism.
16. As a Publisher's developer, I want an event to be able to go both to in-process Consumers and to an external forwarder, so that a broker integration does not displace sibling features.
17. As a solution owner, I want features with no handler for an event to get no inbox row, so that no undispatchable row ever stalls a Group.
18. As a solution owner, I want to version my releases through `<Version>` without orphaning pending inbox rows, so that a deploy never silently stops delivery.
19. As a solution owner, I want the build to fail if any project overrides `AssemblyVersion`, so that nobody breaks the inbox wire format unknowingly.
20. As a solution owner, I want the build to fail when an Integration Event has no handler or no outbox handler, so that "nobody listens" stays a defect, as `MSG0005` makes it.
21. As a solution owner, I want a redelivered outbox row to fan out without duplicating inbox rows, so that the at-least-once outbox never stalls the exactly-once inbox.
22. As a solution owner, I want one shared inbox table with its own migration, so that the inbox's DDL is owned in one place like the outbox's.
23. As a solution owner, I want the migration scripts and CI checks to cover the inbox context like the outbox context, so that its schema cannot drift.
24. As a solution owner, I want the outbox to remain available, so that effects leaving the process keep a durable, at-least-once path.
25. As a solution owner, I want the documented consequence that a Consumer added later never sees earlier events, so that I plan backfills consciously.
26. As a solution owner, I want the documented consequence that a Consumer removing its handler must drain its rows first, so that I don't leave stalled Groups behind.
27. As a solution owner, I want renaming an event, its Contracts project or a Consumer's context documented as a wire-format change, so that I drain before renaming.
28. As a developer adding a feature with `modulith-feature`, I want its context scaffolded ready to consume, so that the new feature can consume events without extra setup.
29. As a developer adding a feature, I want the scaffold to hint where inbox handlers go and how to forward an event externally, so that I follow the pattern without reading an ADR.
30. As an agent working in a generated project, I want a `CLAUDE.md` tripwire saying inbox-driven Commands run inside the inbox transaction and must stay short and local, so that I don't call an external service from one.
31. As an agent working in a generated project, I want the `reaching-another-feature` skill to describe consuming through the inbox, so that I don't reach for a mediator notification handler.
32. As a developer, I want a plain `INotificationHandler` for an Integration Event outside `Infrastructure` to fail the architecture tests, so that a Consumer cannot skip its inbox by accident.
33. As a template maintainer, I want the design's guarantees proven against the real composition and the library's real processor, so that a wiring mistake cannot pass the tests.
34. As a template maintainer, I want the source generator's handling of the shared envelope proven before anything else is built, so that the one unverified assumption fails fast.

## Implementation Decisions

- **Library version.** Stays on `Underground.Outbox` 0.21.1; no library change. Library-side fan-out (Recipients, named Inboxes) may replace the outbox handler's fan-out later without changing how Consumers are written.
- **One shared inbox.** A shared `InboxContext` in the SharedKernel outbox project owns the `shared.inbox` table and its migration, registered with the library's single inbox registration. It is the only context mapping the inbox; feature contexts map only the outbox.
- **Envelope.** One shared `InboxEnvelope<TConsumerContext, TEvent>` carrying the event. Its closed type name is distinct per Consumer, which is what lets the library's one-handler-per-type dispatch fan out. No per-feature or per-event envelope class.
- **Publishing.** Unchanged from before this work: the Publisher stages one outbox row through its own context.
- **Fan-out.** A shared `IntegrationEventOutboxHandler<TEvent>`, derived body-less per event by the publishing feature, publishes the claimed event through the mediator. Each Consumer's handler adds its own row, in a transaction of its own. A unique violation on `shared.inbox` means an earlier delivery already added that Consumer's row, so it reports success.
- **Row identity.** The inbox row's EventId is `{envelope type}_{EventId}` hashed (SHA-256, first 16 bytes) into a Guid: deterministic, so a redelivered fan-out collides with its own rows. The library orders a Group by transaction and append order, never by EventId.
- **Group scope.** The inbox row's GroupKey is the Consumer context's name prefixed to the event's GroupKey, so Groups are per Consumer.
- **Consuming.** A shared abstract `IntegrationEventInboxHandler<TEvent, TContext>` is both the mediator notification handler that adds the Consumer's row and the library's inbox handler for the matching envelope. A Consumer declares one subclass in its Infrastructure layer that maps the event to one of its Commands. Per message it opens a child scope, points the Consumer's context at the inbox's connection (not owned) and transaction, sends the Command through that scope's mediator, and throws on a failed `Result`. `UnitOfWorkBase` joins the open transaction (issue 01, shipped).
- **One reaction per feature per event.** One envelope type has one handler, so a feature reacts to an event with exactly one Command.
- **Refusals retry.** A failed `Result`, including a Business Rule Violation, throws, so the library retries with capped, jittered backoff. A Consumer that wants to accept a refusal models it in its Command.
- **Forwarding.** A plain `INotificationHandler<TEvent>` in an Infrastructure project, run during fan-out, at least once.
- **Wire-format pin.** `AssemblyVersion` is pinned to `1.0.0.0` in the solution's build props, because a closed generic's type name embeds each type argument's assembly version. Owners version through `Version`, `FileVersion` and `InformationalVersion`.
- **Sample.** Payments' current notification handler becomes a `RecordPayment` Command and its handler, without the "check first" guard. Payments' Infrastructure declares the inbox handler mapping Orders' `SomeEntityAdded` event to it.
- **Sub-template.** `modulith-feature` scaffolds hints for the outbox handler of a published event and the inbox handler of a consumed one.
- **Shipped docs.** A generated `CLAUDE.md` tripwire about inbox-driven Commands running inside the inbox transaction; updated `reaching-another-feature` skill, `IIntegrationEvent` remarks and package README. Shipped prose cites no ADR or `.scratch` file.
- **No sample forwarder.** The template ships no Kafka or broker example.

## Testing Decisions

- **A good test** drives the solution the way production does and asserts only what a user of the template could observe: rows in the shared tables, Consumer data, and whether a row completed. It does not assert on transports, routes or registries directly.
- **Primary seam: `SolutionUnderTest` in `ModulithTemplate.TemplateTests`**, the real composition over a real Postgres. It gains one delivery operation that drives the library's own inbox processor, each attempt in a fresh scope, until nothing is claimable, plus a read of the inbox rows beside the existing outbox read. Tests publish by sending Orders' existing command and assert through the existing Payments read.
- **Fault injection** uses the existing transient-failure injector under a retrying execution strategy, as `UnitOfWorkTests` does.
- **A second Consumer that can be told to fail** is declared only in the TemplateTests assembly (a small context plus an inbox transport), which needs the outbox source generator referenced there. That assembly does not ship.
- **Cases**: aggregate and outbox row commit together or not at all; fan-out stages one row per Consumer and none for a feature with no handler; a redelivered outbox row adds only the missing rows; a failing forwarder retries the outbox row and leaves each Consumer one row; one Consumer failing leaves the other committed and completed, and leaves itself incomplete with none of its writes; after a transient replay the Consumer's data exists once and its row is completed; the generator registered the shared envelope's handler under the expected type name.
- **Architecture tests** in `ModulithTemplate.ArchitectureTests`: every Integration Event has an inbox transport subclass or an outbox handler; no project overrides `AssemblyVersion`; every feature context maps the inbox message.
- **Prior art**: `UnitOfWorkTests` (retrying strategy, injected failures), `OutboxAtomicityTests` (one save writes aggregate and row), `IntegrationEventDeliveryTests` (rewritten onto the new seam), `OutboxMappingTests` and `IntegrationEventTests` (architecture rules).
- Payments has no test projects in the template, so `RecordPayment`'s behaviour is covered through the primary seam rather than a feature test project.

## Out of Scope

- Library-side fan-out (Recipients, named Inboxes) or any `Underground.Outbox` change.
- A sample Kafka or broker forwarder.
- Backfilling a newly added Consumer with earlier events.
- Tooling to detect or drain rows of a removed Consumer.
- More than one reaction per feature per event.
- Ordering across different Publishers or different Groups.

## Further Notes

- The single unverified assumption: the source generator registers a body-less subclass of the abstract transport under the closed type name of `InboxEnvelope<TConsumerContext, TEvent>`. Prove it first; if it fails, stop and report rather than falling back to reflection.
- Consumers run inside a write transaction, which holds back `pg_snapshot_xmin` for the whole instance while they run; this is why the shipped tripwire asks them to stay short.
- Supersedes the drafts on `claude/gracious-babbage-vqxvj6` and `claude/sweet-goodall-i2rbo8`.

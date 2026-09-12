# Cross-feature integration events

Status: ready-for-agent

Broken into tickets in `issues/`. Implementation order is 01 and 02 (independent), then 03, then
04-08 (independent of each other).

## Problem Statement

A developer who scaffolds a solution with `dotnet new modulith` gets a modular monolith in which
Features are strictly isolated: separate schemas, separate `DbContext`s, no shared types, with
architecture tests enforcing all of it. The template answers one direction of cross-Feature
communication — a Module API, for "I need your data to finish the request I am in right now" — and
says nothing about the other.

So the moment a developer needs one Feature to *react* to something that happened in another, the
template offers no answer and every available shortcut breaks the architecture it just set up:
referencing the sibling's Application layer (the architecture tests reject it), reaching into the
sibling's schema (silent coupling the tests cannot see), or firing an in-process notification and
hoping (the reaction is lost whenever the process dies between commit and dispatch). The developer
either invents durable messaging themselves — outbox semantics, ordering, redelivery, idempotency —
or quietly abandons the isolation the template exists to provide.

## Solution

The template ships cross-Feature communication as a first-class, durable mechanism: **Integration
Events**.

A Feature raises a Domain Event as it does today. An application-layer handler translates that into
an Integration Event — a record in the Feature's `Contracts` project, written in the Feature's
published language — and hands it to a publisher. The publisher stages a row in a shared outbox
table using the Feature's own `DbContext`, so the event is written by the very same `SaveChanges`
that persists the aggregate: if the business change commits, the event is recorded, and if it rolls
back, so does the event. There is no window in which one exists without the other.

A background worker then picks the row up and re-publishes it in-process, where any number of other
Features receive it through ordinary mediator notification handlers and act on it in their own
`DbContext`, in their own transaction, in a fresh scope.

The developer writes a record, a translating handler, and a consuming handler. Atomicity, ordering,
redelivery, backoff and tracing are the template's problem, not theirs.

## User Stories

1. As a developer, I want one Feature to react to something that happened in another, so that I can
   build a workflow spanning Features without coupling them.
2. As a developer, I want the Integration Event to be recorded atomically with the domain change
   that caused it, so that a crash between the two cannot leave my system inconsistent.
3. As a developer, I want a Feature to publish without knowing who consumes, so that adding a
   consumer never means editing the publisher.
4. As a developer, I want to consume another Feature's event without referencing anything but its
   `Contracts` project, so that the architecture tests keep passing.
5. As a developer, I want to declare an Integration Event as a plain record, so that publishing does
   not require me to learn a messaging framework.
6. As a developer, I want the translation from Domain Event to Integration Event to live in one
   place per event, so that the mapping cannot drift between the paths that trigger it.
7. As a developer, I want to publish by calling one method from a Domain Event handler, so that no
   publish site has to remember transaction handling.
8. As a developer, I want events concerning the same aggregate delivered in order, so that a later
   event cannot overtake an earlier one about the same thing.
9. As a developer, I want events concerning unrelated aggregates delivered concurrently, so that one
   slow consumer does not stall the whole application.
10. As a developer, I want a failed delivery retried automatically with backoff, so that a transient
    outage does not lose the event.
11. As a developer, I want to know that delivery is at-least-once, so that I write my handler to
    tolerate seeing an event twice rather than assuming it will not happen.
12. As a developer, I want to know that a sibling consumer's failure will re-run my successful
    handler, so that "idempotent" means the right thing when I write it.
13. As a developer, I want the event to carry a stable identity, so that I can deduplicate on it
    when my handler cannot be made naturally idempotent.
14. As a developer, I want a documented recipe for exactly-once consumption, so that a handler that
    genuinely cannot be idempotent still has a supported path.
15. As a developer, I want my consuming handler to run in its own scope with its own `DbContext`, so
    that consuming does not entangle my transaction with the publisher's.
16. As a developer, I want a consumer to be able to publish its own Integration Event, so that a
    chain of reactions across three Features is expressible.
17. As a developer, I want the build to fail when an Integration Event has no consumer, so that a
    contract nobody listens to is caught rather than shipped.
18. As a developer, I want the architecture tests to reject an Integration Event declared outside a
    `Contracts` project, so that I cannot accidentally publish my internal domain model.
19. As a developer, I want a naming convention enforced on Integration Events, so that they are
    recognisable as contracts wherever they appear.
20. As a developer, I want a working example in the generated solution, so that I can copy a correct
    implementation rather than reconstruct one from prose.
21. As a developer, I want the example to span two real Features, so that it demonstrates the thing
    it claims to demonstrate.
22. As a developer scaffolding a new Feature, I want its outbox wiring generated for me, so that I
    cannot forget the part that fails only at runtime.
23. As a developer, I want `dotnet new modulith-feature` to produce a Feature that can publish
    immediately, so that adding the second Feature to a solution is not a research task.
24. As a developer, I want the event delivered promptly after commit rather than at the next poll,
    so that the system feels responsive in development.
25. As a developer, I want delivery to still happen if the prompt trigger is missed, so that
    liveness never depends on a notification arriving.
26. As a developer, I want the event's trace context carried to the consumer, so that one
    distributed trace spans publish and consume.
27. As a developer, I want my consumer's failure logged with the event's identity, so that I can
    find the failing message.
28. As an operator, I want a warning when a message has failed repeatedly, so that a permanently
    stuck event surfaces in existing alerting rather than silently blocking its Group.
29. As an operator, I want the outbox table's migration to be applied by the same command as every
    other migration, so that deployment gains no new step.
30. As an operator, I want an idempotent SQL script for the shared schema attached to the release,
    so that the outbox deploys the same way Feature schemas already do.
31. As a template maintainer, I want CI to catch a model change with no migration for the outbox
    context, so that the shared schema cannot drift.
32. As a template maintainer, I want the solution to build warning-free with `-warnaserror`, so that
    the new projects hold the same bar as the rest.
33. As a template maintainer, I want the scaffolded output to contain no trace of `ModulithTemplate`
    or `FeatureName`, so that the token rename is complete.
34. As a template maintainer, I want the new mechanism unit-testable without a database, so that CI
    stays free of a Postgres service container.
35. As a template maintainer, I want the design's reasoning recorded, so that a future contributor
    does not "fix" the shared outbox table back into per-Feature tables.

## Implementation Decisions

### Shape

- A **Domain Event stays internal**; an application-layer handler translates it into an **Integration
  Event**. The domain model never learns that siblings or a delivery mechanism exist. This follows
  ADR 0002 and standard DDD layering: handling domain events is an application concern.
- An Integration Event is a `record` in its owning Feature's `Contracts` project, under `Events/`,
  named `*IntegrationEvent`. Exactly one Feature owns and may publish a given event.
- Consumption is an ordinary mediator notification handler in a consuming Feature's `Application`
  layer, so no new consumer-side concept is introduced.

### Abstractions and placement

- `IIntegrationEvent` (a mediator notification carrying `EventId` and `GroupKey`) and
  `IIntegrationEventPublisher` live in `SharedKernel.Application` — **not** `SharedKernel.Domain`,
  where `IRepository` and `IUnitOfWork` sit, because a repository is a domain abstraction and an
  event publisher is not.
- Each Feature declares its own `I<Name>IntegrationEventPublisher` marker in its **`Application`**
  layer, mirroring how `I<Name>Repository` binds a DI registration to one `DbContext` and avoiding
  the "last Feature registered wins" failure.
- A new `SharedKernel.Outbox` project holds the outbox `DbContext`, its migration, the publisher base
  class and the in-process republisher, keeping the outbox package out of `SharedKernel.Application`
  and therefore out of every `Contracts` project.
- **Consequence to watch:** the architecture tests forbid `Infrastructure` depending on
  `Application`, so the per-Feature publisher is bound in the Feature's `Web` composition root
  (`<Name>Module.cs`), not beside the repository in `Configure<Name>Infrastructure`.

### Transport

- One shared outbox table in its own schema, per ADR 0001. Every Feature `DbContext` implements the
  library's outbox context interface and maps the message entity to that shared table with
  `ExcludeFromMigrations()`; only the outbox context owns the DDL.
- Publishing **stages** the row rather than saving it, so it is written by the Feature's own
  `SaveChanges` alongside the aggregate — one transaction, no explicit transaction handling in
  Feature code. This depends on `Underground.Outbox` ≥ v0.16.0, whose `StageMessage` neither saves
  nor requires an open transaction.
- The connection sets the search path to the shared schema, because the library's claim SQL names
  its tables unqualified. Feature tables remain schema-qualified and are unaffected.
- Staging outside an explicit transaction triggers no push-based processing, so the save path
  explicitly asks the outbox to process after a successful save; the polling interval remains the
  liveness guarantee.
- The shared registration that adds a Feature's `DbContext` also attaches the library's
  save-changes interceptor, so this is not a per-Feature responsibility.

### Delivery semantics

- **At least once.** One outbox message fans out to all consumers through the mediator; a consumer
  that throws fails the message, and redelivery re-runs consumers that already succeeded. Handlers
  must be idempotent in that strong sense.
- One message per consumer is explicitly rejected: it would require the publisher to enumerate its
  subscribers.
- No inbox by default. A consumer needing exactly-once wraps the event in a message type of its own
  and takes an inbox row for it — documented, not scaffolded, because a shared inbox row per event
  id would let the first consumer to complete starve the rest.
- Group key is the aggregate the event concerns, declared on the event itself so it cannot be
  forgotten. The library's `"default"` group key is explicitly not used: it serialises everything and
  lets one stuck message block the entire application.
- Event identity is declared on the Integration Event, not carried as transport metadata, because the
  mediator republish would otherwise drop it before any consumer saw it.

### Registration

- Outbox handler registrations are contributed from each Feature's `Configure<Name>Application`,
  because that is the only layer permitted to reference `Contracts`. The closed generic republisher
  is registered directly — no empty derived class per event.
- The host references the outbox source generator, which must live in the DI root project.

### Scaffolding and tooling

- `dotnet new modulith-feature` scaffolds the outbox context mapping, the publisher marker interface
  and its binding. No new projects, so `primaryOutputs`/`primaryOutputIndexes` are untouched.
- `add-migration.sh` special-cases the outbox context. `update-database.sh` and the CI
  pending-model-changes check already discover contexts from the host container and need no change;
  the release script's per-schema loop does.
- A second `Payments` Feature is added to the generated solution, consuming an `Orders` event and
  persisting a row, because cross-Feature communication cannot be demonstrated with one Feature.

## Testing Decisions

A good test here asserts **externally observable behaviour** — that publishing a command results in
an outbox row carrying the right contract, that a consumer's write lands — and never that a
particular method was called on a particular collaborator. The delivery machinery itself (claiming,
leases, backoff, ordering under concurrency) is the library's to test and is not re-tested here.

**Proposed primary seam — one, and the highest available.** Drive a real command through the real
mediator pipeline against a Feature `DbContext` backed by EF Core SQLite in-memory, and assert that
the outbox row was written **by the same save** as the aggregate. One test crosses the domain event
being raised, the dispatch interceptor, the translating handler, the publisher, staging, and the
save boundary — which is exactly where this design's subtlety lives. It needs no Postgres, so CI
stays as it is. *This is a change from the earlier decision to unit-test the seams in isolation, and
is the one point in this spec to confirm before implementation.*

**Existing seam, extended.** The ArchUnitNET suite gains two rules: an Integration Event must live in
a `Contracts` assembly, and must follow the `Events/` placement and `*IntegrationEvent` naming
convention — bidirectional, matching how `NamingConventionTests` already works. Prior art:
`ContractIsolationTests`, which exists for exactly this class of gap.

**Substitute-based unit tests**, in the style of the existing `ExceptionBehaviourTests` /
`ValidationBehaviourTests` (xUnit v3 + NSubstitute), only for what the primary seam cannot reach:
the republisher forwarding to the mediator, and the repeated-failure warning threshold.

**These tests do not ship.** They verify the *template's* design, not behaviour a generated
project's owner maintains, so a scaffolded solution should not carry them. The project lives in the
content test tree — inheriting the shared build props and running under the existing
`cd working/content/modulith && dotnet test` — and is removed at scaffold time by a `template.json`
`modifiers.exclude`, with its `.slnx` entry wrapped in a template-engine conditional. Verified by
experiment: the engine processes `<!--#if -->` conditionals in `.slnx`, and because those markers are
ordinary XML comments the solution still builds normally in this repository. The gating symbol is a
constant rather than a user-facing parameter, so it cannot be switched on from the command line.
Note that the excluded files are still *packed* into the `.nupkg` — exclusion happens at scaffold
time, not pack time — so exclude them from the pack glob too if package size matters.

**Scaffold verification** stays manual and is documented: generate a solution, generate a Feature
into it, build with `-warnaserror`, and confirm no `ModulithTemplate` or `FeatureName` token
survives.

## Out of Scope

- **Contract versioning.** The event's full type name is a wire contract and renaming one orphans
  stored rows. Deliberately unaddressed in this version — no guard, no documented migration path.
- **Exactly-once consumption.** Documented as a recipe; not scaffolded, not tested.
- **A health check or dashboard** for outbox depth or stuck Groups. Only the repeated-failure warning
  is in scope.
- **Integration tests against real Postgres**, and the CI service container they would require.
- **Out-of-process transports.** The republish is in-process only; the outbox is what makes it
  durable, not a step toward a message broker.
- **Scheduled or delayed events**, though the library supports them.
- **Ordering guarantees across aggregates.** Only within a Group.
- **A Blazor page for the consuming Feature.**
- **Shipping the atomicity tests to generated projects.** They are maintainer-facing and excluded
  from the scaffold.

## Further Notes

- `Underground.Outbox` 0.16.0 is published and pinned in central package management. Its
  `StageMessage(IOutboxDbContext, OutboxMessage)` is synchronous and takes no cancellation token,
  since it only stages an entity and performs no I/O.
- The library's source generator emits code that assumes `ImplicitUsings` is enabled; the generated
  dispatcher does not qualify `Task`, `CancellationToken` or `Action<>`. This solution enables it
  globally, so it is a non-issue here — worth knowing before anyone turns it off.
- One behaviour could not be verified by reading and is deliberately designed around rather than
  relied on: whether EF's implicit `SaveChanges` transaction raises the library's commit hook. The
  explicit process-after-save call makes the answer irrelevant to correctness — worst case it is
  redundant.
- Verified by experiment during design, not assumed: a per-entity table-and-schema mapping overrides
  the context's default schema, and an entity added during the saving-changes interceptor is
  persisted by that same save in a single round trip.
- Terms used here are defined in `CONTEXT.md`; the two decisions this spec implements are recorded in
  `docs/adr/0001` and `docs/adr/0002`.

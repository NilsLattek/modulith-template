# 03: Orders publishes an integration event, Payments consumes it

**What to build:** the tracer bullet. Something happens in `Orders`, and `Payments` reacts to it
durably — if the Orders change commits, the Payments reaction happens, even across a process
restart between the two.

An Orders aggregate raises a Domain Event as it does today. An application-layer handler translates
that into an Integration Event — a record in the Orders `Contracts` project, in Orders' published
language — and hands it to a publisher, which stages a row in the shared outbox using **Orders' own
`DbContext`**, so the row is written by the same save that persists the aggregate. A worker then
picks the row up and republishes it in-process, where a `Payments` handler receives it in a fresh
scope and writes through its own context.

This is one ticket rather than a publish half and a consume half because an Integration Event with no
consumer fails the host build, so the publishing side cannot land green on its own.

Placement follows ADR 0002: the event abstractions are application-layer, not domain. Watch the
consequence — the layering rules forbid a Feature's `Infrastructure` from depending on its
`Application`, so the per-Feature publisher is bound in the Feature's `Web` composition root, not
beside the repository.

**Blocked by:** 01 (outbox storage foundation), 02 (Payments Feature).

**Status:** done

- [x] Shared abstractions exist for an Integration Event and for publishing one, carrying the event's
      own identity and its Group key so neither can be forgotten at a publish site
- [x] Every Feature's context implements the library's outbox context interface and maps the message
      entity to the shared table, excluded from that Feature's own migrations, done once in the shared
      registration rather than per Feature
- [x] `Orders` declares an Integration Event in `Contracts` and a handler that translates its Domain
      Event into it
- [x] Publishing stages the outbox row rather than saving it, so no Feature code opens a transaction
- [x] `Payments` consumes the event and persists a row through its own context
- [x] Outbox handler registrations are contributed from each Feature's Application configuration, the
      only layer permitted to reference `Contracts`; no empty derived class per event
- [x] The worker is asked to process after a successful save, since staging outside an explicit
      transaction triggers no push-based processing
- [x] Running the app end to end, an Orders command results in a Payments row
- [x] `dotnet build -warnaserror` is clean and `dotnet test` passes

## Comments

**Implemented** on `claude/awesome-ramanujan-54ymto`, on top of 01 and 02.

The path end to end: `SomeEntity` is now an `AggregateRoot` and raises `SomeEntityAddedDomainEvent`;
`SomeEntityAddedDomainEventHandler` (Orders `Application/DomainEventHandlers/`) translates it into
`SomeEntityAddedIntegrationEvent` (Orders `Contracts/Events/`) and hands it to
`IOrdersIntegrationEventPublisher`, which stages an outbox row through `OrdersContext`;
`SomeEntityAddedIntegrationEventHandler` (Payments `Application/IntegrationEventHandlers/`) consumes
it and writes a `Payment`.

### The one design change: the source generator cannot dispatch to a generic republisher

The spec has the closed generic republisher registered directly, with the library's generated
dispatcher routing to it. **It cannot.** The generator routes by scanning for handler *classes* that
name a concrete message type, and two experiments settled it:

- An open generic `Republisher<TEvent> : IOutboxMessageHandler<TEvent>` in a scanned assembly makes
  it emit `typeof(TEvent)` into the host — **the host build fails** with `CS0246`.
- An empty class closing the generic by inheritance (`FooRepublisher : Republisher<FooEvent>`) is
  **not discovered at all**: only directly-declared interfaces are read. So the "empty derived class
  per event" the ticket rejects would not have worked either.

What the generator *does* do is useful: it resolves handlers as
`GetRequiredService<IOutboxMessageHandler<TEvent>>()`, i.e. by interface. So `SharedKernel.Outbox`
supplies its own `IMessageDispatcher<OutboxMessage>` — `IntegrationEventRepublisher` — which looks
the row's type name up in an `IntegrationEventRegistry` each feature contributes to from its
`Configure<Name>Application`, deserializes, and publishes to the mediator. One republisher for every
event, no per-event class, no `[assembly: ContainsOutboxHandlers]` to forget. It is registered in
`Program.cs` *after* `AddOutboxServices`, which is what makes it win; `AddOutboxServices` itself is
generated into the host and so cannot be called from `SharedKernel.Outbox`.

Consequence worth knowing: a plain `IOutboxMessageHandler<T>` a user writes is no longer dispatched.
The republisher throws a `ParsingException` naming the unregistered type, so it fails loudly.

### Also here

- **Orders now ships a migration.** The end-to-end check needs an `orders` table, so `InitialOrders`
  was generated — which settles the asymmetry 02 flagged as needing a deliberate decision, in favour
  of scaffolding both. `README.md`, `CLAUDE.md` and the `OrdersSummary` error message no longer tell
  the user to create it.
- **`SomeEntity` gained an `Amount`.** Payments' `Payment` requires a positive amount, so without one
  the worked example would have had to invent a constant. Command, validator and tests follow.
- The outbox mapping is applied once, in `AddModuleDbContext`, through an `IModelCustomizer`
  replacement (`OutboxModelCustomizer`) that acts only on a context implementing `IOutboxDbContext`.
  A feature declares nothing but the `DbSet`. Confirmed: `InitialOrders` creates `orders.some_entity`
  and no outbox table, and all three contexts report no pending model changes.

### Verified against a real Postgres, not just the suite

- A command produces an `orders.some_entity` row, a completed `shared.outbox` row whose `group_key`
  is the aggregate id and whose `type` is the contract's full name, and a matching `payments.payment`
  row. The stored payload omits `GroupKey` (it is `[JsonIgnore]` and derived).
- **Durability across a restart:** with the app stopped, an order and its outbox row were committed by
  hand; on restart, with no command sent, the payment appeared and the row completed.
- **The push after save is real, not the poll covering for it.** With `ProcessingDelayMilliseconds`
  set to five minutes, a hand-inserted row sat undelivered for 10s, while a row staged by a real
  command was delivered within ~4s — and woke the worker enough to drain the older row too.
- **Redelivery is idempotent:** a second outbox row for the same order left exactly one payment.

That last one matters because the E2E caught a real bug the unit tests could not:
`PaymentForOrderSpec` originally built its criteria in a `new`-shadowed `Query` property instead of
the constructor, which leaves an Ardalis specification with **no criteria at all** — so `AnyAsync`
matched any payment and the consumer skipped every order after the first. Fixed, and
`PaymentForOrderSpecTests` evaluates the spec in memory; reverting the fix fails it.

`dotnet build -warnaserror` clean; `dotnet test` 120/120. A scaffolded `MyApp` builds clean, passes
120/120, and takes a `modulith-feature` on top and still builds, with no `ModulithTemplate`,
`FeatureName` or `ModulithApp` token surviving.

### Two things a review caught, both fixed

- The registry was only registered as a side effect of the first `AddIntegrationEvent<T>()`. Deleting
  the sample event — which the scaffold's docs invite — would have left the worker failing to resolve
  `IntegrationEventRegistry` every poll instead of reporting an unregistered type.
  `AddIntegrationEventDelivery()` now registers it unconditionally.
- The library's `ProcessMessagesOnSaveChangesInterceptor` holds what a transaction staged in plain
  instance fields, so one scoped instance shared by two feature contexts would let the second discard
  the first's pending push. It is now constructed per context — the same reason
  `DomainEventDispatchInterceptor<TContext>` is generic.

### For the tickets that follow

- **04** (atomicity tests): no SQLite package is referenced yet; `Directory.Packages.props` will need
  `Microsoft.EntityFrameworkCore.Sqlite`. The seam it wants is exactly the one exercised above.
- **06** (repeated-failure warning): `IntegrationEventRepublisher` receives the whole `OutboxMessage`,
  so `RetryCount` is in hand — no plumbing needed.
- **07** (sub-template outbox wiring): a feature scaffolded by `modulith-feature` still builds, but its
  context does **not** implement `IOutboxDbContext` and it has no publisher marker or binding. That is
  07's to add; the three places are the context, `I<Name>IntegrationEventPublisher`, and the
  `AddScoped` in `<Name>Module.cs`.
- **08** (docs): `CLAUDE.md` in the content tree still says nothing about integration events. Only the
  statements my change made false were corrected here.

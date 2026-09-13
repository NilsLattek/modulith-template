# 07: The feature sub-template scaffolds outbox wiring

**What to build:** a Feature created with `dotnet new modulith-feature` can publish an Integration
Event without the developer wiring anything up by hand.

The per-Feature pieces are mechanical and one of them is unforgiving: a context that does not map the
outbox message entity to the shared table compiles, starts, and fails at its first claim. That is
exactly the kind of thing a template should own rather than document.

Use the `Payments` Feature from 02 and 03 as the reference for what the sub-template must emit; the
two should end up identical in this respect.

No new projects are involved, so the sub-template's output list and its post-action indexes are
untouched — this is content only.

**Blocked by:** 03 (Orders publishes an integration event, Payments consumes it).

**Status:** done

- [x] A scaffolded Feature's context implements the outbox context interface and maps the message
      entity to the shared table, excluded from its own migrations
- [x] A scaffolded Feature declares its publisher marker and binds it in its composition root
- [x] A scaffolded Feature's Application configuration has the registration hook present but empty
- [x] Scaffolding a solution, then a Feature into it, builds with `-warnaserror`
- [x] Nothing named `FeatureName` or `ModulithApp` survives

## Comments

**Implemented** on `claude/awesome-ramanujan-54ymto`.

Four pieces per Feature, in the sub-template and in `Payments`, which was the reference but had only
the first of them:

- `<Name>Context` implements `IOutboxDbContext` and declares the `OutboxMessage` `DbSet`. The mapping
  onto `shared.outbox` and the exclusion from the Feature's own migrations come from
  `AddModuleDbContext`, so the Feature declares nothing else — confirmed by generating a migration
  for a scaffolded Feature: it is empty, and no context reports pending model changes.
- `I<Name>IntegrationEventPublisher` in `Application`, and `<Name>IntegrationEventPublisher` in `Web`
  binding it to the Feature's own context.
- The `AddScoped` for that pair in `<Name>Module.cs` — in `Web`, not `Configure*Infrastructure`,
  because the marker lives in `Application` and a Feature's `Infrastructure` may not reference it.
- `Configure<Name>Application` gained a block body carrying the `AddIntegrationEvent<T>()` hook as a
  comment, with the `SharedKernel.Application.Events` using already in place so the hinted call
  compiles as written.

`Web` picks up the `Underground.Outbox` package and the `SharedKernel.Outbox` project reference;
`Infrastructure` picks up `Underground.Outbox`. Normalised for the rename tokens, every one of these
files is now byte-identical between `Payments` and the sub-template except the schema name.

### One test, in the project that does not ship

`PerFeaturePublisherTests` (AtomicityTests) resolves `IPaymentsIntegrationEventPublisher` from the
real composition and asserts the staged row lands on `PaymentsContext` and not on `OrdersContext`.
It is asserted through `Payments` precisely because `Payments` publishes nothing of its own — the
shape the sub-template scaffolds. Verified it fails on the realistic mistake: binding the publisher
to a sibling's context still compiles, and the test goes red.

### Verified

`dotnet build -warnaserror` clean; `dotnet test` 136/136. A scaffolded solution takes a
`modulith-feature` on top, builds `-warnaserror` clean and passes 131/131, with no `ModulithTemplate`,
`FeatureName` or `ModulithApp` token surviving.

### For 08 (docs)

The content tree's `CLAUDE.md` still says nothing about integration events, and now also nothing
about what a scaffolded Feature arrives already wired for.

# 09: Remove the central republisher

**What to build:** each Feature listens to the outbox rows carrying the Integration Events its own
`Contracts` declares, and forwards them to the mediator itself.

`IntegrationEventRepublisher` is one `IMessageDispatcher<OutboxMessage>` for the whole application,
registered after `AddOutboxServices` so as to displace the library's own, resolving a row's type
through an `IntegrationEventRegistry` every Feature feeds. Issue 03 records why: in
`Underground.Outbox` 0.16 the source generator discovered handlers from the composition root's
compilation only, and no generic republisher could be one.

`Underground.Outbox` 0.17 discovers handlers per assembly instead — every project declaring them
emits its own `Add<Assembly>MessageHandlers()`, called by the owning module — and `AddOutboxServices`
became an ordinary library method. A per-event handler in the publishing Feature is then exactly what
the library wants, and the republisher, the registry and the registration-order rule all become dead.

**Blocked by:** 03 (Orders publishes an integration event, Payments consumes it).

**Status:** done

- [x] `Underground.Outbox` and its source generator are on 0.17.0
- [x] `SomeEntityAddedOutboxHandler : IOutboxMessageHandler<T>` lives in
      `Orders.Application/OutboxHandlers/` and publishes to the mediator
- [x] `ConfigureOrdersApplication` calls the generated registration method; the source generator is
      referenced by the projects that declare handlers, not by the DI root
- [x] `IntegrationEventRepublisher`, `IntegrationEventDelivery`, `IntegrationEventLog`,
      `IntegrationEventRegistry` and `IntegrationEventRegistration` are gone
- [x] A staged row still reaches the consuming Feature, and an unclaimed type still fails loudly
- [x] `NamingConventionTests` governs `*OutboxHandler` → `Application.OutboxHandlers` both ways
- [x] The sub-template scaffolds the packages and the hint, and adds no project
- [x] ADR 0003 records the decision; the generated project's CLAUDE.md matches the new shape
- [x] `dotnet build -warnaserror` is clean and `dotnet test` passes

## Comments

Reverts 06 (repeated-failure warning): the threshold check lived in the republisher, and duplicating
it into every Feature's handler buys less than it costs. The library still reports an unclaimed type,
and a stalled Group shows up as unhandled-row depth.

Two package pins had to move with it: `Underground.Outbox` 0.17.0 depends on
`Microsoft.Extensions.Hosting` 10.0.12, so the centrally pinned `Microsoft.Extensions.*` at 10.0.11
became `NU1109` downgrades and were bumped to 10.0.12.

The generated method is named after the assembly with non-alphanumerics stripped, so the call site
reads `AddModulithTemplateFeaturesOrdersApplicationMessageHandlers()` — which `sourceName` rewrites
in step with the assembly name it is derived from. Verified by scaffolding, not by reading.

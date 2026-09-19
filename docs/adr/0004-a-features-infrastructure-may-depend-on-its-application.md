# A feature's Infrastructure may depend on its Application

`FeatureLayerTests` used to forbid a feature's `Infrastructure` from naming its `Application`, in
both directions. That is stricter than the architecture the template otherwise follows, and it cost
one concrete thing: `<Name>IntegrationEventPublisher` implements `I<Name>IntegrationEventPublisher`,
an `Application` port (ADR 0002), so the only place it could be built was the feature's `Web`
composition root — an adapter over `DbContext` and `IOutbox` living in the Razor project, away from
the repository it is a sibling of and away from the outbox handlers that read the rows it stages.

The rule is now asymmetric: `Infrastructure` may name `Application`; `Application` may not name
`Infrastructure`.

## Considered options

**Keep the symmetric ban.** Ports would all have to live in `Domain` for an adapter to implement
them, which ADR 0002 already rejects for this one: a publisher of integration events is not a domain
abstraction the way a repository is.

**Classic Evans layering**, as in the .NET microservices guidance: `Application` depends on
`Domain` *and* `Infrastructure`. Rejected — it inverts the direction this template is built on, and
would put EF Core within reach of every command handler.

**Clean/Onion**, as in eShopOnWeb and the Ardalis template: *"Infrastructure services and
repositories should implement interfaces that are defined in the Application Core, and so
Infrastructure should have a reference to the Application Core project."* Chosen: it is what the
template already is everywhere else — ports inward, adapters outward, `Web` as composition root, and
`Application` free of EF Core.

## Consequences

The publisher moves to `<Name>.Infrastructure/Events/` and is registered in
`Configure<Name>Infrastructure` beside `I<Name>Repository<T>` and `I<Name>UnitOfWork`. A feature's
`Web` project sheds its `Underground.Outbox` package reference and its `SharedKernel.Outbox` project
reference; it is Razor components and the module's composition root again, nothing else.

**Only the closed direction is enforced.** Nothing stops an adapter from reaching a command handler,
a validator or an integration event handler in `Application` — a narrower rule was considered and
rejected as more maintenance than the mistake is worth, since `PerFeaturePublisherTests` and
`ContractIsolationTests` already catch the failures that actually bite. The generated project's
`CLAUDE.md` states the intent instead; review holds the line.

`FeatureLayerTests.Infrastructure_does_not_depend_on_Web` is renamed from
`Infrastructure_depends_only_on_Domain`, which the change would otherwise have turned into a lie.

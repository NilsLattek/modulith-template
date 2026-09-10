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

**Status:** ready-for-agent

- [ ] Shared abstractions exist for an Integration Event and for publishing one, carrying the event's
      own identity and its Group key so neither can be forgotten at a publish site
- [ ] Every Feature's context implements the library's outbox context interface and maps the message
      entity to the shared table, excluded from that Feature's own migrations, done once in the shared
      registration rather than per Feature
- [ ] `Orders` declares an Integration Event in `Contracts` and a handler that translates its Domain
      Event into it
- [ ] Publishing stages the outbox row rather than saving it, so no Feature code opens a transaction
- [ ] `Payments` consumes the event and persists a row through its own context
- [ ] Outbox handler registrations are contributed from each Feature's Application configuration, the
      only layer permitted to reference `Contracts`; no empty derived class per event
- [ ] The worker is asked to process after a successful save, since staging outside an explicit
      transaction triggers no push-based processing
- [ ] Running the app end to end, an Orders command results in a Payments row
- [ ] `dotnet build -warnaserror` is clean and `dotnet test` passes

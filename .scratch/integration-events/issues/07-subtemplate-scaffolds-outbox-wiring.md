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

**Status:** ready-for-agent

- [ ] A scaffolded Feature's context implements the outbox context interface and maps the message
      entity to the shared table, excluded from its own migrations
- [ ] A scaffolded Feature declares its publisher marker and binds it in its composition root
- [ ] A scaffolded Feature's Application configuration has the registration hook present but empty
- [ ] Scaffolding a solution, then a Feature into it, builds with `-warnaserror`
- [ ] Nothing named `FeatureName` or `ModulithApp` survives

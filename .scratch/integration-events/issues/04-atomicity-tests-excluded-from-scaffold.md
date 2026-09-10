# 04: Atomicity tests, excluded from scaffolded projects

**What to build:** a test proving the claim the whole design rests on — that the outbox row and the
aggregate change are written by **one** save — and which never appears in a generated project.

Drive a real command through the real mediator pipeline against a Feature context backed by an
in-memory relational provider, and assert the outbox row is there afterwards. This crosses the Domain
Event being raised, the dispatch interceptor, the translating handler, the publisher, staging, and
the save boundary — the one seam where this design's subtlety actually lives. A substitute-based test
could only assert that a method was called, which is an implementation detail, not the behaviour.

These tests verify the template's design, not behaviour a generated project's owner maintains, so
they must not ship. Keep the project in the content test tree — it inherits the shared build props
and runs under the existing test command — and remove it at scaffold time.

Verified during design: the template engine processes conditionals in `.slnx`, and because the
markers are ordinary XML comments the solution still builds normally in this repository.

**Blocked by:** 03 (Orders publishes an integration event, Payments consumes it).

**Status:** ready-for-agent

- [ ] A test asserts the aggregate row and the outbox row are persisted by a single save
- [ ] A test asserts nothing is written when the save is not performed
- [ ] The republisher forwarding an event to consumers is covered
- [ ] The tests run under the existing `dotnet test` invocation, with no database server required
- [ ] Scaffolding a solution produces no trace of the test project, and its solution entry is gone
- [ ] The gating symbol is a constant, not a user-facing parameter, so it cannot be switched on
- [ ] The scaffolded solution builds with `-warnaserror`

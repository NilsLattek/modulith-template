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

**Status:** done

- [x] A test asserts the aggregate row and the outbox row are persisted by a single save
- [x] A test asserts nothing is written when the save is not performed
- [x] The republisher forwarding an event to consumers is covered
- [x] The tests run under the existing `dotnet test` invocation, with no database server required
- [x] Scaffolding a solution produces no trace of the test project, and its solution entry is gone
- [x] The gating symbol is a constant, not a user-facing parameter, so it cannot be switched on
- [x] The scaffolded solution builds with `-warnaserror`

## Comments

**Implemented** on `claude/awesome-ramanujan-54ymto`, as `test/ModulithTemplate.AtomicityTests/`.

`SolutionUnderTest` composes the solution exactly as `Program.cs` does — the real feature modules,
`AddOutboxServices` then `AddIntegrationEventDelivery` in that order, the real mediator options — and
then moves every context onto one in-memory SQLite connection. The move is the one part worth
knowing about: `SqliteRebinding.OnSqlite<TContext>` resolves the options the solution registered and
rebuilds them, carrying every non-provider extension across, so the interceptors under test are the
ones `AddModuleDbContext` attached rather than a copy written here. Hand-writing that list would have
let a change to the shared registration break the host while these tests still passed.

The tests: one save carries both the `SomeEntity` row and the `OutboxMessage` row (asserted on the
save itself, not just on the rows that ended up in the database); the staged row carries the
aggregate as its Group key; staging without a save writes nothing; the dispatcher the worker resolves
is `IntegrationEventRepublisher`; a staged row driven through it lands a `Payment`; and an
unregistered type throws rather than completing the row quietly.

Both mutations were checked to fail the suite: a publisher that saves on its own (two recorded saves)
and `AddModuleDbContext` without the domain event dispatch interceptor (three tests red).

### Two things the design notes did not anticipate

- **The library defaults two outbox columns in Postgres SQL** — `clock_timestamp()` and
  `pg_current_xact_id()` — which SQLite has no answer for, so the fixture registers a user function
  for each on the connection. Nothing in these tests reads either column.
- **The SQLite package pin is a trace too.** `Directory.Packages.props` would otherwise ship a
  `Microsoft.EntityFrameworkCore.Sqlite` version nothing references, so its line is wrapped in the
  same `<!--#if (includeTemplateTests) -->` conditional as the `.slnx` entry. The engine processes
  conditionals in `.props` just as it does in `.slnx`.

The gate is a `generated`/`constant` symbol, so `dotnet new modulith` has no switch for it. Excluded
files are still packed into the `.nupkg` (exclusion happens at scaffold time); the pack glob was left
alone, as the spec allows.

Verified: `dotnet build -warnaserror` clean and `dotnet test` 126/126 in this repository; a scaffolded
`MyApp` has no `test/MyApp.AtomicityTests/`, no solution entry, no conditional markers and no SQLite
pin, builds `-warnaserror` clean, passes 120/120, and still takes a `modulith-feature` on top and
builds.

# Integration events are staged into the shared inbox and applied exactly once in each Consumer's own context

Supersedes ADR 0001 and ADR 0002's delivery model. An integration event used to travel through the
shared outbox and fan out through the mediator, so delivery was at least once and a Consumer that
threw re-ran every sibling. Every Feature shares one database, so a sibling's tables are not
"outside" it and the outbox step bought nothing. The Publisher now stages the event into a shared
`shared.inbox` table in the same `SaveChanges` as its aggregate, and `Underground.Outbox` (from
0.22.0) writes one row per Consumer handler. Each Consumer applies it in its own transaction, exactly
once, and one Consumer failing neither repeats nor holds back another.

## Considered options

**Keep the outbox and have its handler write the inbox rows.** A second, at-least-once step whose
redelivery collides with rows it already wrote.

**Every feature context claims for itself, filtered by Recipient.** Workers and connections per
feature, and a filter that defeats the library's windowed claim.

**Fan-out by a feature-name key the template passes.** A string to keep in step; the library knows
every handler already, so the handler class itself is the Recipient.

## Consequences

**The Consumer's context is enlisted onto the inbox's connection and transaction.** ADR 0001
rejected this for the outbox because every feature's `UnitOfWorkBase` could break it; here it lives
once, in `IntegrationEventInboxTransport`. Without it the Consumer's writes would commit on their
own connection, outside the savepoint and outside exactly-once.

**Each call opens a child scope.** A retrying execution strategy (Aspire's default) replays the
whole inbox attempt in the same scope; a Consumer context left over from the rolled-back attempt
still tracks its `Added` entities as `Unchanged` and would silently skip re-inserting them. A fresh
scope gives the replay a fresh context, dispatcher and interceptors. `UnitOfWorkBase` joins an open
transaction for the same reason.

**Consumers are `IIntegrationEventHandler<T>`, not mediator notifications.** An event still
publishable through the mediator would bypass the inbox silently. That costs `MSG0005`'s build-time
"no Consumer" check; the library's Stage-time throw and an architecture test requiring a transport
per integration event replace it.

**Consumers run inside a write transaction.** It holds `pg_snapshot_xmin` back for the whole
Postgres instance while it runs, and a replay runs it again, so Consumers stay short and touch only
their own data; anything leaving the database is an outbox message they stage.

**The handler class name is a wire contract**, like the event's: renaming a `…InboxHandler` orphans
its pending rows.

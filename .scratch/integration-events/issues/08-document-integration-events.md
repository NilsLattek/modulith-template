# 08: Document integration events for the generated project

**What to build:** a developer working in a generated solution can find out how to make one Feature
react to another, and learns the two things they will otherwise get wrong.

The generated project's own `CLAUDE.md` describes the architecture for its consumer. It currently
says `Contracts` is how one Feature reaches another and documents only the synchronous Module API.
It needs the asynchronous half.

Two points carry most of the value and neither is guessable from the code:

- Delivery is at least once, and a consumer must tolerate re-running after a **sibling** consumer
  failed — stronger than what "idempotent" usually implies. Include the escape hatch for a consumer
  that genuinely cannot be idempotent.
- An Integration Event's full type name is a wire contract. Renaming or moving one orphans the rows
  already stored under the old name and stalls their Group.

Write it for the template's consumer, not for whoever maintains the template. Keep to the repository's
comment discipline: short, and only where the code cannot say it.

**Blocked by:** 03 (Orders publishes an integration event, Payments consumes it).

**Status:** done

- [x] The generated project's `CLAUDE.md` explains when to use an Integration Event rather than the
      Module API, and where each piece lives
- [x] It states the at-least-once guarantee and what idempotency has to mean for a consumer
- [x] It documents the recipe for a consumer that cannot be idempotent
- [x] It warns that renaming an Integration Event is a breaking change
- [x] It notes that an Integration Event with no consumer fails the build, and why that is deliberate

## Comments

**Implemented** on `claude/awesome-ramanujan-54ymto`. Documentation only — no code changed.

`working/content/modulith/CLAUDE.md` gains a **Reaching another feature** section, placed after the
CQRS material it leans on and before `### Infrastructure`. The one-line `Contracts` sentence in the
Architecture intro now points at it.

- **Which route.** A table contrasting the Module API ("I need the answer to finish this request")
  with an Integration Event, both through `Contracts`.
- **Where each piece lives.** A table walking the shipped `Orders` → `Payments` path: domain event,
  contract record, translating handler, `AddIntegrationEvent<T>()`, consumer — plus the prose that
  the row is *staged* on the aggregate's own save, and that consuming costs a `ProjectReference` and
  a handler, nothing more.
- **Idempotency in the strong sense.** Stated as tolerating a re-run because a *sibling* failed,
  with the shipped `PaymentForOrderSpec` check as the cheap route and `EventId` for when no natural
  key exists.
- **The escape hatch**, per ADR 0002: the fan-out handler writes only an inbox row keyed on
  `EventId`, and the unrepeatable effect hangs off that row's own save with exactly one consumer.
  Noted honestly that this leaves the effect retried only on its own failure — at-least-once cannot
  become exactly-once outside the database — and that the shared outbox must not be used as an
  inbox.
- **Renaming is breaking**, because the row stores the full type name; the failure mode (Group
  blocked, `No integration event is registered as …`, repeated-failure warning) is named.
- **No consumer fails the build.**

### One correction the code forced

`MSG0005` is a **warning**, not an error. Verified by dropping an unconsumed `IIntegrationEvent` into
`Orders.Contracts/Events/` and building the host: `warning MSG0005: MediatorGenerator found message
without any registered handler`, `0 Error(s)`. `Directory.Build.props` sets
`CodeAnalysisTreatWarningsAsErrors=false` and nothing promotes warnings globally, so it is
`-warnaserror` — what both CI workflows and the documented build command pass — that turns it into a
failure. The docs say exactly that rather than the flat "fails the build" the ticket and ADR 0002
use; the ADR's claim is true of CI but not of a bare `dotnet build`.

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

**Status:** ready-for-agent

- [ ] The generated project's `CLAUDE.md` explains when to use an Integration Event rather than the
      Module API, and where each piece lives
- [ ] It states the at-least-once guarantee and what idempotency has to mean for a consumer
- [ ] It documents the recipe for a consumer that cannot be idempotent
- [ ] It warns that renaming an Integration Event is a breaking change
- [ ] It notes that an Integration Event with no consumer fails the build, and why that is deliberate

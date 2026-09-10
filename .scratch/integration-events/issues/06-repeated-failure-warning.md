# 06: Repeated-failure warning

**What to build:** an operator can see that an Integration Event is stuck before it silently blocks
everything behind it.

A permanently failing message blocks its Group forever and the library states plainly that detecting
that is the host's job. The republisher already receives the message's retry count on every attempt,
so a warning above a threshold turns a silent stall into something existing alerting can pick up.

Keep it to a log. A health check or a depth dashboard is more machinery than the template should
presume, and is explicitly out of scope.

**Blocked by:** 03 (Orders publishes an integration event, Payments consumes it).

**Status:** ready-for-agent

- [ ] A message whose retry count is at or past the threshold logs a warning naming the event type
      and its identity
- [ ] Below the threshold, nothing is logged at warning level
- [ ] The behaviour is covered by a test
- [ ] `dotnet build -warnaserror` is clean

# Business rule violations are thrown as a BusinessException and converted to a Result by the pipeline

A Business Rule Violation has to reach the user as a stable, localizable code. The Application
layer already speaks FluentResults, so the obvious alternative was for the domain to return
`Result`s as well. We chose instead for the domain (and, for checks needing a lookup, the handler) to
throw `BusinessException(code, message)`, and for `ExceptionBehaviour` to convert it into a
`BusinessError` on the handler's own result type — the approach ABP takes.

Throwing keeps `SharedKernel.Domain` free of FluentResults, works from constructors and factories
that cannot return a result, matches the `ArgumentException` guards the entities already throw, and
rolls back the save for free when the violation is raised inside a domain event handler. Conversion
lives in the one behaviour that already turns exceptions into results.

## Consequences

- The type separates what the user may see from what they may not: only a `BusinessError`'s message
  is shown; an `ExceptionalError`'s can leak internals and stays behind fixed text.
- A violation is logged as a warning by `LoggingBehaviour`, not as an error with a stack trace.
- It only works through the mediator, and only for handlers returning a result — the same limit
  every pipeline behaviour has. A `BusinessException` thrown anywhere else is an ordinary unhandled
  exception.
- The template ships codes but no localization; each generated project resolves codes to text itself.

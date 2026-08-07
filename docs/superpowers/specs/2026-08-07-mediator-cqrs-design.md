# Mediator-based CQRS from Web to Application

**Date:** 2026-08-07
**Scope:** `working/content/modulith/` (the solution template) and `working/content/feature/` (the feature sub-template)

## Context

A feature's `Web` layer currently has no documented way to invoke its `Application` layer beyond
`CLAUDE.md`'s `I*AppService` convention — and that convention has no implementation anywhere in the
template. `FluentResults` and `Riok.Mapperly` are pinned in `Directory.Packages.props` but referenced
by no project; `SomeEntity` is an empty placeholder; there is no app service and no `Services/`
folder. The Orders feature demonstrates *structure*, not behaviour.

This change introduces [martinothamar/Mediator](https://github.com/martinothamar/Mediator) so that a
feature's `Web` layer dispatches commands and queries to handlers in its `Application` layer, and so
that every message passes through pipeline behaviours for logging and exception-to-`Result`
conversion. Because there are no app services to migrate, the change is purely additive in code —
but it does replace the documented `I*AppService` convention.

## Goals

- `Web` → `Application` communication happens through `IMediator.Send`, never a direct service call.
- Every message is wrapped in a logging behaviour.
- Unhandled exceptions become `Result.Fail` centrally instead of in per-handler `try`/`catch`.
- Both templates scaffold the convention, and the architecture tests enforce it.

## Non-goals

- **Notifications / domain events / integration events.** `INotification`, domain-event dispatch on
  `SaveChanges`, and cross-feature integration events are out of scope and get their own spec. The
  `.IntegrationEvents` exemption already present in `FeatureModuleTests` stays dormant.
- **Validation behaviour.** No FluentValidation dependency and no validator-per-command convention.
- **A runtime-executable example.** `SomeEntity` stays unmapped; see "Worked example".

## Decisions

| Decision | Choice |
|---|---|
| Handlers vs. app services | Handlers **replace** `I*AppService`; the concept is removed from docs and arch tests |
| Home for the behaviours | The host, `src/ModulithTemplate.Web/Behaviours/` — no new shared project |
| Marker interfaces | None written; `ICommand<T>`/`IQuery<T>`/`ICommandHandler<,>`/`IQueryHandler<,>` ship in `Mediator.Abstractions` |
| Behaviours shipped | `LoggingBehaviour` + `ExceptionBehaviour` |
| Layout | Folder per operation: `Commands/PlaceOrder/{PlaceOrderCommand,PlaceOrderCommandHandler}.cs` |
| Mediator version | 3.0.2 (3.1.0 is still rc) |
| Service lifetime | `Scoped` |

`Application.Common` was considered as a home for the behaviours and rejected: nothing in a feature's
`Application` project consumes them, so the project would exist only to hold two classes that only the
composition root uses. `Web.Common` was rejected because every feature's `Web` project references it
and would inherit `FluentResults` and `Mediator.Abstractions` for no reason.

## Architecture

### Package additions (`content/modulith/Directory.Packages.props`)

| Package | Version | Referenced by |
|---|---|---|
| `Mediator.SourceGenerator` | 3.0.2 | host only (`PrivateAssets="all"`) |
| `Mediator.Abstractions` | 3.0.2 | host, every feature `Application` and `Web` |
| `FluentResults` | 4.0.0 (already pinned) | host, every feature `Application` |
| `Microsoft.Extensions.Diagnostics.Testing` | 10.8.0 | `ModulithTemplate.WebTests` only |

`Microsoft.Extensions.Diagnostics.Testing` follows the `dotnet/extensions` version line (10.8.0), not
the runtime line (10.0.10) — the two are unrelated and both are current.

`Microsoft.Extensions.Logging.Abstractions` is deliberately **not** added: the host is a
`Microsoft.NET.Sdk.Web` project, so `ILogger<T>` and the `[LoggerMessage]` generator arrive with the
ASP.NET Core shared framework, and `WebTests` inherits that framework reference transitively.

### Project changes

| Project | Change |
|---|---|
| `ModulithTemplate.Web` (host) | `+ Mediator.SourceGenerator`, `+ Mediator.Abstractions`, `+ FluentResults`; `+ InternalsVisibleTo`; new `Behaviours/`; `AddMediator(…)` in `Program.cs` |
| `ModulithTemplate.Web.Common` | `ServiceScopeExtensions` gains two `ValueTask` overloads |
| `<Name>.Application` | `+ Mediator.Abstractions`, `+ FluentResults`; `+ InternalsVisibleTo`; `Commands/`, `Queries/`, `Dtos/` |
| `<Name>.Web` | `+ Mediator.Abstractions` |
| `<Name>.Domain`, `<Name>.Infrastructure` | unchanged |

No new source project. `Domain` acquires no dependency on the mediator.

**`InternalsVisibleTo` is a consequence of `internal sealed` handlers**, not an incidental extra. A
feature's `ApplicationTests` project constructs its handlers directly, and the host's `WebTests`
project constructs the behaviours directly; neither can see an `internal` type without it. Both
templates therefore add an `InternalsVisibleTo` MSBuild item naming the matching test project —
`ModulithTemplate.Features.Orders.ApplicationTests`, `ModulithApp.Features.FeatureName.ApplicationTests`,
and `ModulithTemplate.WebTests` respectively. The tokens inside those items are renamed at scaffold
time like any other file content. The alternative — making handlers `public` — would give up the
compile-time guarantee that `Web` cannot reach past `IMediator`.

### Host registration

The source generator runs only in the outermost executable, so `Program.cs` is the one place
`AddMediator` can live:

```csharp
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(LoggingBehaviour<,>), typeof(ExceptionBehaviour<,>)];
});
```

`ServiceLifetime.Scoped` is required, not stylistic: handlers inject the feature repositories, which
are bound to a scoped `DbContext`. It also makes `IMediator` scoped, which fits the existing
`WithNewScopeAsync` component pattern — the mediator is resolved inside the fresh scope, never from
the root provider.

Behaviour order is `Logging` outermost, `Exception` inner. `ExceptionBehaviour` logs the exception at
`Error` with its stack trace and then converts, so `LoggingBehaviour` observes a uniform `Result`
outcome for every message — success, domain failure, or converted exception alike — and never has to
reason about raw exceptions.

**Handlers bypass the per-feature `Configuration.cs` convention.** The generated `AddMediator`
discovers handlers across referenced assemblies (host → `Orders.Web` → `Orders.Application`), so a
feature's handlers register themselves with no host edit, but they are *not* registered by
`ConfigureOrdersApplication`. This is a deliberate deviation from the "register services in the
owning layer's `Configuration.cs`" rule and must be stated in `CLAUDE.md`.

### Behaviours

Both live in `src/ModulithTemplate.Web/Behaviours/`.

```csharp
internal sealed class LoggingBehaviour<TMessage, TResponse>(ILogger<…> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
```

Unconstrained on `TResponse`, so it logs every message regardless of return type — logging must never
be conditional on the shape of a response. It times the call, and distinguishes outcomes with
`response is IResultBase { IsFailed: true }`.

```csharp
internal sealed class ExceptionBehaviour<TMessage, TResponse>(ILogger<…> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : ResultBase<TResponse>, new()
{
    // catch (OperationCanceledException) { throw; }
    // catch (Exception ex) => new TResponse().WithError(new ExceptionalError(ex))
}
```

The `TResponse` constraint is what makes generic failure construction type-safe. FluentResults
parameterises each result type by itself — `Result : ResultBase<Result>` and
`Result<TValue> : ResultBase<Result<TValue>>`, both with a public parameterless constructor — so
`ResultBase<TResult>.WithError(IError)` returns the concrete type it was called on. `new TResponse()`
builds an empty (therefore successful, since `IsFailed => Reasons.OfType<IError>().Any()`) result, and
`.WithError(…)` flips it to failed and returns `TResponse`. No cast, no reflection, checked at compile
time. The alternative — `(TResponse)(object)Result.Fail(…)` — compiles and then throws
`InvalidCastException` whenever `TResponse` is `Result<T>`.

The constraint also determines **which messages the behaviour applies to**: a handler returning
`ValueTask<int>` does not satisfy it, so the behaviour is never inserted into that pipeline and the
exception escapes to the Blazor circuit. That is why "every handler returns `Result`" below is a rule
and not a preference.

Log calls go through a separate non-generic `internal static partial class` of `[LoggerMessage]`
methods taking `ILogger` as the first parameter. Source-generated logging keeps the analyzers quiet,
and the non-generic host class avoids the logging generator's limitations with generic containing
types.

### Blazor call pattern

`IMediator.Send` returns `ValueTask<T>`, but `ServiceScopeExtensions.WithNewScopeAsync` currently
accepts only `Func<IServiceProvider, Task<T>>`, which would force a trailing `.AsTask()` at every call
site. Two `ValueTask` overloads are added to `ModulithTemplate.Web.Common`; the lambda's return type
disambiguates overload resolution, so this is purely additive:

```csharp
var result = await ScopeFactory.WithNewScopeAsync(sp =>
    sp.GetRequiredService<IMediator>().Send(new PlaceOrderCommand(dto), CancellationToken.None));
```

## Conventions

### Layout and visibility

```
Orders.Application/
  Commands/PlaceOrder/
    PlaceOrderCommand.cs          public sealed record  : ICommand<Result<OrderDto>>
    PlaceOrderCommandHandler.cs   internal sealed class : ICommandHandler<PlaceOrderCommand, Result<OrderDto>>
  Queries/GetOrderById/
    GetOrderByIdQuery.cs          public sealed record  : IQuery<Result<OrderDto>>
    GetOrderByIdQueryHandler.cs   internal sealed class
  Dtos/OrderDto.cs
  Mappers/OrderMapper.cs
```

Messages are public records; handlers are `internal sealed`, so a feature's `Web` layer can construct
a message but cannot reach past `IMediator` to call a handler directly. That is compile-enforced and
needs no architecture rule.

### Response contract

Every handler returns `Result` or `Result<T>`. A handler returning anything else sits outside
`ExceptionBehaviour`'s reach (see above).

### Error and logging contract

| Situation | Handled by | Log level |
|---|---|---|
| Expected domain failure | handler returns `Result.Fail("…")` | `Warning` (LoggingBehaviour) |
| Unhandled exception | `ExceptionBehaviour` converts to a failed `TResponse` | `Error`, with stack trace |
| `OperationCanceledException` | rethrown untouched — cancellation is not a failure | none |
| Start of handling | — | `Debug` |
| Success | — | `Information`, with elapsed ms |

## Architecture test changes

All in `test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs`. The `NamingConvention`
record's single `TypeSuffix` becomes `string[] TypeSuffixes`, and `TypeNamePattern` emits an
alternation:

```
.*(Command|CommandHandler)(`\d+)?$
```

because a `Commands` namespace legitimately holds two suffixes. The trailing group is the existing
tolerance for the CLR arity suffix on generic types and is retained verbatim.
The existing conventions become single-element arrays; the surrounding mechanism is
untouched, including `WithoutRequiringPositiveResults()` — which matters here, since a freshly
scaffolded feature has no commands yet.

- **Removed:** the `app service` convention and its two facts. Handlers replace app services, so a
  rule steering types into `Application.Services` would contradict the docs.
- **Added:** `command` → `Application.Commands` / `*Command`, `*CommandHandler`; `query` →
  `Application.Queries` / `*Query`, `*QueryHandler`; `dto` → `Application.Dtos` / `*Dto`. Each in both
  directions, matching every existing convention: six new facts.

The `dto` convention is included because the `Commands` namespace rule admits only `*Command` and
`*CommandHandler` types, so putting `PlaceOrderDto` in the operation folder — the obvious thing to
try — now fails a test. Giving DTOs an enforced home makes that a rule users meet head-on rather than
trip over.

`FeatureLayerTests` and `FeatureModuleTests` need no changes: commands live in `Application`, which
`Web` may already reference, and handlers introduce no cross-feature dependency.

## Worked example

`SomeEntity` is mapped by nothing — `OrdersContext` declares no `DbSet<SomeEntity>`,
`ApplyConfigurationsFromAssembly` finds no configuration, and `Data/Migrations/` holds only a
`.gitkeep`. An example that reached the database would require mapping the entity plus a committed
migration, which `build.yml`'s `has-pending-model-changes` check makes non-optional once a `DbSet`
appears. The example therefore follows the template's existing character: real code that compiles and
is unit-tested against a substituted repository, with no database dependency — the same bargain
`OrdersApplicationSmokeTests` already strikes.

```
Orders.Application/
  Queries/GetSomeEntityCount/
    GetSomeEntityCountQuery.cs          : IQuery<Result<int>>
    GetSomeEntityCountQueryHandler.cs   → IOrdersRepository<SomeEntity>.CountAsync
  Commands/AddSomeEntity/
    AddSomeEntityCommand.cs             : ICommand<Result>
    AddSomeEntityCommandHandler.cs      → IOrdersRepository<SomeEntity>.AddAsync
```

Two operations rather than one, because `ICommand<Result>` and `IQuery<Result<T>>` are meaningfully
different shapes and a template should show both.

## Testing

- **`Orders.ApplicationTests`** — one test per handler against an NSubstitute'd
  `IOrdersRepository<SomeEntity>`. The existing DI smoke test stays (it still proves
  `ConfigureOrdersApplication` composes); its "replace this with a real app-service test" comment is
  updated to refer to handlers.
- **`test/ModulithTemplate.WebTests`** (new project, added to `.slnx`, references
  `src/ModulithTemplate.Web`) — the behaviours:
  - `ExceptionBehaviour` converts a thrown exception to a failed `Result`
  - `ExceptionBehaviour` converts a thrown exception to a failed `Result<T>`
  - `ExceptionBehaviour` rethrows `OperationCanceledException`
  - `ExceptionBehaviour` passes a successful result through untouched
  - `LoggingBehaviour` logs success at `Information` and a failed `Result` at `Warning`

  Log assertions use `FakeLogger<T>`/`Collector` from `Microsoft.Extensions.Diagnostics.Testing`;
  asserting against a substituted `ILogger.Log` means matching on the generic state parameter and is
  brittle. This project carries its own `PackageReference` for that, departing from the
  "a test csproj holds nothing but a `ProjectReference`" norm in `test/Directory.Build.props`.

Behaviours are exercised by constructing them directly with a stub `next` delegate. A full-pipeline
integration test is not attempted: the generated `AddMediator` exists only in the host assembly and
bakes its behaviour list in at generation time. `ModulithTemplate.WebTests` is nonetheless the only
project where such a test could ever live, which is a further argument for the host as the
behaviours' home.

## Template mechanics

Every change lands in both templates.

| File | Change |
|---|---|
| `content/modulith/Directory.Packages.props` | three new `PackageVersion` entries (table above) |
| `content/modulith/ModulithTemplate.slnx` | `+ test/ModulithTemplate.WebTests` under `/test/` |
| `content/modulith/src/ModulithTemplate.Web/` | `Behaviours/`, `Program.cs`, csproj references |
| `content/modulith/src/ModulithTemplate.Web.Common/Extensions/ServiceScopeExtensions.cs` | `ValueTask` overloads |
| `content/modulith/src/Features/Orders/…Application/` | csproj references, worked example, `Dtos/.gitkeep` |
| `content/modulith/src/Features/Orders/…Web/` | csproj reference |
| `content/modulith/test/…ArchitectureTests/NamingConventionTests.cs` | convention changes |
| `content/modulith/test/…Orders.ApplicationTests/` | handler tests |
| `content/feature/…FeatureName.Application.csproj` | `+ Mediator.Abstractions`, `+ FluentResults` |
| `content/feature/…FeatureName.Web.csproj` | `+ Mediator.Abstractions` |
| `content/feature/…FeatureName.Application/` | `+ Commands/.gitkeep`, `Queries/.gitkeep`, `Dtos/.gitkeep` |

Neither `template.json` changes: no project is added to the feature scaffold, so `primaryOutputs` and
the `addProjectsToSolution` post-action are untouched. The token rules still hold — nothing new
introduces an identifier that should be renamed per project without the `ModulithTemplate` /
`FeatureName` / `ModulithApp` prefix.

## Documentation changes (`content/modulith/CLAUDE.md`)

1. The `<Name>.Application` architecture bullet is rewritten around commands, queries, and handlers;
   `I*AppService` is removed from it. Mapperly and the FluentResults contract stay.
2. A new "CQRS and the mediator pipeline" subsection covers the host-level `AddMediator`, the
   `Scoped` lifetime requirement, behaviour ordering, and the handlers-register-themselves deviation
   from the `Configuration.cs` rule.
3. "Database access from Blazor components" switches its snippet to `IMediator`.
4. The Conventions section gains the `Commands` / `Queries` / `Dtos` naming rules and notes they are
   enforced by `NamingConventionTests`.
5. "Adding a feature" notes the scaffolded `Commands/`, `Queries/`, and `Dtos/` folders.

The repo-root `CLAUDE.md` needs no change: no project count or template mechanic it documents changes.

## Risks to verify during implementation

1. **`-warnaserror` over generated code.** `Mediator.g.cs` lands in the host's compilation, where
   Meziantou, SonarAnalyzer, and Roslynator all run. Generated files normally carry an
   `<auto-generated>` header that exempts them, but that must be confirmed against this analyzer set.
   Fallback: a scoped `.editorconfig` entry marking the generated path as generated code.
2. **The `TResponse` constraint.** The Mediator README documents constrained open-generic behaviours
   and notes that returning a response from an exception handler "requires you to know something about
   `TResponse`", but only demonstrates constraints on `TMessage`. If the generator does not honour a
   `TResponse` constraint when selecting behaviours, the fallback is an unconstrained behaviour that
   checks `response is IResultBase` at runtime and rethrows when it cannot convert.
3. **Transitive pinning.** `CentralPackageTransitivePinningEnabled` is on, so any `PackageVersion`
   pinned below a package's own transitive requirement surfaces as NU1109.
   `Microsoft.Extensions.Diagnostics.Testing` 10.8.0 sits on the `dotnet/extensions` line and may
   require `Microsoft.Extensions.*` packages newer than the 10.0.10 runtime-line pins already in
   `Directory.Packages.props`. If NU1109 appears, raise the offending pin rather than disabling
   transitive pinning.

## Verification

- `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror` — clean.
- `cd working/content/modulith && dotnet test` — green, including the new and changed arch tests.
- `dotnet new install working/content/modulith`; scaffold to a temp dir; build with `-warnaserror`;
  confirm no surviving `ModulithTemplate` token.
- `dotnet new install working/content/feature`; scaffold a feature into that generated solution; wire
  it into the host; build with `-warnaserror`; confirm no surviving `FeatureName` or `ModulithApp`
  token.
- Add a trivial command and handler to the scaffolded feature, build, and confirm the handler appears
  in the generated `Mediator.g.cs` registrations — proving discovery reaches a newly scaffolded
  feature's assembly without a host edit beyond the documented `ProjectReference`.

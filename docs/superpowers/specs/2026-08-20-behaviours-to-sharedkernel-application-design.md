# Move the mediator behaviours and ValidationError into SharedKernel.Application

Date: 2026-08-20
Scope: `working/content/modulith/` (solution template) and `working/content/feature/` (feature sub-template)

## Goal

Make the three mediator pipeline behaviours **referenceable by any host**, not just
`ModulithTemplate.Web`. A future API or console host still configures its own pipeline —
`options.PipelineBehaviors` stays a hand-written list per host — but today it cannot even name the
types, because they are `internal` to the Blazor host.

Secondary, and independently justified: `ValidationError` lives in `SharedKernel.Web`, which
feature `Application` projects do not reference. A handler therefore cannot produce a
`ValidationError` even when a failure is semantically a field error. Moving it to
`SharedKernel.Application` fixes that.

Non-goal: automatic pipeline registration, a shared `AddModulithPipeline` helper, or a second host.

## Decisions

### Placement: `SharedKernel.Application`

Behaviours go to `SharedKernel.Application/Behaviours/`, `ValidationError` to
`SharedKernel.Application/Errors/`.

Rejected: a fifth `SharedKernel.*` project. `SharedKernel.Application` currently holds exactly one
file (`Events/IDomainEventHandler.cs`); its csproj comment advertising "the two event-handler
interfaces, the published-contract marker, and the queue handlers" is stale. Behaviours-in-Application
is the conventional Clean Architecture placement, and a fifth shared project for three files is not
worth it in an already project-heavy template.

**Accepted cost:** `Contracts` projects reference `SharedKernel.Application`, so they inherit
FluentResults, FluentValidation and `Logging.Abstractions` transitively. Nothing breaks — every
project that uses those already declares them explicitly — but `Orders.Contracts.csproj`'s
"deliberately references nothing but SharedKernel.Application" comment is weakened and must be
reworded honestly. Not mitigated with `PrivateAssets`, which risks runtime assets not being copied.

### Telemetry: wildcard subscription, constant inlined

`ServiceDefaults/Extensions.cs` subscribes with a prefix instead of a named constant:

```csharp
.AddSource("ModulithTemplate.*")   // replaces .AddSource(ActivitySources.Mediator)
```

This is the only viable direction. `LoggingBehaviour` needs the source name at its
`static readonly ActivitySource` field, so leaving the constant in `ServiceDefaults` would create
`SharedKernel.Application -> ServiceDefaults`, dragging ASP.NET Core and OpenTelemetry into the
innermost ring and thus into every `Contracts` project.

`ServiceDefaults` ends with **zero solution-specific types** — closer to upstream Aspire, which its
header comment says is the point.

`ActivitySources.cs` is **deleted outright**, not relocated. The constant existed to keep the
producer and the subscriber in sync; a prefix subscription removes that coupling entirely, since any
name under `ModulithTemplate.*` is exported. A drifted literal can no longer silently stop tracing.
The two remaining literals — `LoggingBehaviour` and `LoggingBehaviourTests` — fail loudly on drift:
a wrong name records no span and the assertion fails.

The value drops the now-inaccurate `Web` segment:
`"ModulithTemplate.Web.Mediator"` -> `"ModulithTemplate.Mediator"`.

`Extensions.cs:76`'s `AddSource(builder.Environment.ApplicationName)` is **kept**. It is now
redundant (`ModulithTemplate.Web` matches the wildcard), but it is upstream Aspire code and the file
is explicitly maintained as a small diff against upstream. Redundant subscription is harmless.

### Tests: new `SharedKernel.ApplicationTests` project

`test/SharedKernel/ModulithTemplate.SharedKernel.ApplicationTests/`, mirroring `src/SharedKernel/`.
The template's stated convention is one test project per project; leaving behaviour tests in
`WebTests` would leave a visible exception at the exact spot being touched.

`test/Directory.Build.props` resolves `xunit.runner.json` via `$(MSBuildThisFileDirectory)`, so the
new depth needs no build changes.

## Work

### `src/SharedKernel/ModulithTemplate.SharedKernel.Application/`

- New `Behaviours/`: `LoggingBehaviour.cs`, `ExceptionBehaviour.cs`, `ValidationBehaviour.cs`,
  `BehaviourLog.cs`. No constants class — `LoggingBehaviour` holds
  `private static readonly ActivitySource ActivitySource = new("ModulithTemplate.Mediator");`.
- New `Errors/ValidationError.cs`.
- Behaviours `internal sealed` -> `public sealed` (the host names them by type in `AddMediator`).
  `BehaviourLog` stays `internal`.
- Add explicit `using Microsoft.Extensions.Logging;` to the logging-touching files: the Web SDK
  supplies that implicitly, plain `Microsoft.NET.Sdk` does not.
- csproj: add `FluentResults`, `FluentValidation`, `Microsoft.Extensions.Logging.Abstractions`.
  Rewrite the leading comment — it is stale today and wrong after this change.
- `ValidationError`'s `<remarks>` currently justifies its `SharedKernel.Web` home. Rewrite: the
  reason is now that a handler can produce one, which it could not before.
- Verify `System.Diagnostics.ActivitySource` resolves in a plain-SDK net10.0 library without a
  `System.Diagnostics.DiagnosticSource` package reference. Expected to; the build settles it.

### `src/ModulithTemplate.Web/`

- `Program.cs`: swap `using ModulithTemplate.Web.Behaviours;` for the new namespace. The
  `PipelineBehaviors` list and its ordering comment are unchanged.
- Delete `Behaviours/`.
- csproj: drop the `FluentValidation` PackageReference if nothing else in the project uses it.
- Re-check `InternalsVisibleTo Include="ModulithTemplate.WebTests"` — likely unused once the
  behaviours leave. Remove if so.

### `src/ModulithTemplate.ServiceDefaults/`

- Delete `ActivitySources.cs`.
- `Extensions.cs`: `.AddSource(ActivitySources.Mediator)` -> `.AddSource("ModulithTemplate.*")`, and
  update the surrounding comment plus the deviation list in the file header (lines 4-5).

### `src/SharedKernel/ModulithTemplate.SharedKernel.Web/`

- Delete the now-empty `Errors/` folder. The project keeps its `FluentResults` reference for
  `ServiceScopeExtensions`; verify before removing anything.

### Tests

- New `test/SharedKernel/ModulithTemplate.SharedKernel.ApplicationTests/` + `.slnx` entry under a new
  `/test/SharedKernel/` folder.
- Move `LoggingBehaviourTests.cs`, `ExceptionBehaviourTests.cs`, `ValidationBehaviourTests.cs` there;
  update namespaces and usings. `LoggingBehaviourTests.cs:34`'s `ActivitySources.Mediator` becomes the
  literal `"ModulithTemplate.Mediator"`; drop its `using ModulithTemplate.ServiceDefaults;`.
- Move the `Microsoft.Extensions.Diagnostics.Testing` PackageReference from `WebTests` to the new
  project if `WebTests` no longer needs `FakeLogger`.
- `ServiceDefaultsTests.cs:84` stays in `WebTests`, with `ActivitySources.Mediator` replaced by the
  literal `"ModulithTemplate.Mediator"` and `using ModulithTemplate.ServiceDefaults;` dropped if
  otherwise unused. Its role narrows: it proves the wildcard subscription works at all, rather than
  pinning one exact name.

### Templating and docs

- `working/content/feature/.../Components/_Imports.razor:7` — `@using ModulithApp.SharedKernel.Web.Errors`
  points at a namespace that ceases to exist; a scaffolded feature would fail to compile. Repoint at
  `ModulithApp.SharedKernel.Application.Errors`. Apply the same check to the Orders `_Imports.razor`
  in the solution template.
- Generated `CLAUDE.md`: the validation section names
  `ModulithTemplate.SharedKernel.Web/Errors/ValidationError.cs`. Update the path, and add one line
  recording that a solution ActivitySource must be named `ModulithTemplate.*` to be exported — that
  convention is no longer discoverable from a constants class in `ServiceDefaults`.
- `Orders.Contracts.csproj`: reword the isolation comment per the accepted cost above.

## Out of scope

Architecture-test coverage of `SharedKernel`. `SolutionAssemblies` loads only assemblies containing
`.Features.`, and `NamingConventionTests` documents that shared projects are deliberately excluded
because they define abstractions with the same suffixes. So no existing rule is tripped by this move,
and extending ArchUnitNET to the shared projects is a separate, larger change.

## Verification

1. `dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror` — clean.
2. `cd working/content/modulith && rtk proxy dotnet test` — all green, and the new project's tests
   actually run (the "zero tests ran" trap).
3. `dotnet run` the host, exercise a failing validation, confirm one `ModulithTemplate.Mediator` span
   per command reaches the collector. The wildcard subscription is the one change no unit test can
   fully prove.
4. Scaffold clean: `dotnet new modulith -n MyApp -o /tmp/MyApp`, build it, then
   `dotnet new modulith-feature --appName MyApp -n Payments` into it and build again. Confirm no
   `ModulithTemplate`, `FeatureName` or `ModulithApp` string survives, and that the scaffolded
   feature's `_Imports.razor` compiles.

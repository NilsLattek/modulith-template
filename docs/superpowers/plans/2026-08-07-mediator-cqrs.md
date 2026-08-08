# Mediator CQRS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a feature's `Web` layer dispatch commands and queries to handlers in its `Application` layer via [martinothamar/Mediator](https://github.com/martinothamar/Mediator), with every message wrapped in logging and exception-to-`Result` pipeline behaviours.

**Architecture:** The mediator source generator runs only in the host (`ModulithTemplate.Web`), so a single `AddMediator` call there discovers handlers across every referenced feature assembly and installs the two behaviours, which live in the host beside it. Feature `Application` projects declare public `ICommand`/`IQuery` records and `internal sealed` handlers returning FluentResults `Result`; feature `Web` projects send those messages through `IMediator`. Architecture tests enforce the naming and placement of the new building blocks.

**Tech Stack:** .NET 10, Mediator 3.0.2, FluentResults 4.0.0, Ardalis.Specification, xUnit v3 on Microsoft.Testing.Platform, NSubstitute, `Microsoft.Extensions.Diagnostics.Testing` (FakeLogger), ArchUnitNET.

**Spec:** `docs/superpowers/specs/2026-08-07-mediator-cqrs-design.md`

## Global Constraints

- **No git actions.** Both `CLAUDE.md` files state the maintainer handles all commits, branches, and releases. Every task therefore ends in a **Checkpoint** step (build + test) instead of a commit. Do not run `git add`, `git commit`, `git branch`, or `git push`.
- **All work happens under `working/content/`.** `working/content/modulith/` is the solution template; `working/content/feature/` is the feature sub-template. Every convention change must land in both.
- **Token discipline.** `sourceName` is `ModulithTemplate` in the solution template; the feature sub-template uses `FeatureName` (`sourceName`) and `ModulithApp` (the `appName` parameter). Any new identifier that should be renamed per project must carry the right prefix — including inside `.csproj` files, whose contents are token-replaced too.
- **Builds must be warning-clean under `-warnaserror`.** Meziantou, SonarAnalyzer, and Roslynator run on build with `EnforceCodeStyleInBuild`. Suppress narrowly with `#pragma warning disable <id>` plus a matching restore, never globally.
- **Package versions are centrally managed.** Add every new package to `working/content/modulith/Directory.Packages.props` as a `PackageVersion`; `PackageReference` entries in `.csproj` files carry no `Version` attribute. `CentralPackageTransitivePinningEnabled` is on, so a pin set below a package's own transitive requirement surfaces as NU1109.
- **Tests run from the content directory.** `cd working/content/modulith && dotnet test`. Running `dotnet test` against the `.slnx` from the repo root silently runs zero tests, because `global.json` opts into Microsoft.Testing.Platform and is resolved from the current directory.
- **Code style:** 4-space indent, primary constructors where possible, XML comments on public types and members, test methods named `<MethodName>_<Conditions>_<AssertedOutcome>` in snake_case with no `Async` suffix, Arrange/Act/Assert with a comment per section.
- **Exact versions:** Mediator 3.0.2 (`Mediator.Abstractions`, `Mediator.SourceGenerator`), `Microsoft.Extensions.Diagnostics.Testing` 10.8.0. FluentResults 4.0.0 is already pinned. `Microsoft.Extensions.Logging.Abstractions` is deliberately **not** added — the host is a `Microsoft.NET.Sdk.Web` project and gets `ILogger<T>` plus the `[LoggerMessage]` generator from the ASP.NET Core shared framework.

---

## File Structure

**Created**

| File | Responsibility |
|---|---|
| `modulith/src/ModulithTemplate.Web/Behaviours/BehaviourLog.cs` | Source-generated log messages shared by both behaviours. Non-generic on purpose. |
| `modulith/src/ModulithTemplate.Web/Behaviours/ExceptionBehaviour.cs` | Converts an unhandled exception into a failed `Result`/`Result<T>`. |
| `modulith/src/ModulithTemplate.Web/Behaviours/LoggingBehaviour.cs` | Times every message and logs its outcome. |
| `modulith/test/ModulithTemplate.WebTests/ModulithTemplate.WebTests.csproj` | Host test project — the only place the behaviours are visible. |
| `modulith/test/ModulithTemplate.WebTests/ExceptionBehaviourTests.cs` | Exception → failed result, cancellation passthrough, success passthrough. |
| `modulith/test/ModulithTemplate.WebTests/LoggingBehaviourTests.cs` | Log level per outcome, via `FakeLogger`. |
| `modulith/test/ModulithTemplate.WebTests/ServiceScopeExtensionsTests.cs` | The new `ValueTask` overloads. |
| `modulith/src/.../Orders.Application/Queries/GetSomeEntityCount/GetSomeEntityCountQuery.cs` | Worked query message. |
| `modulith/src/.../Orders.Application/Queries/GetSomeEntityCount/GetSomeEntityCountQueryHandler.cs` | Worked query handler. |
| `modulith/src/.../Orders.Application/Commands/AddSomeEntity/AddSomeEntityCommand.cs` | Worked command message (non-generic `Result`). |
| `modulith/src/.../Orders.Application/Commands/AddSomeEntity/AddSomeEntityCommandHandler.cs` | Worked command handler. |
| `modulith/test/.../Orders.ApplicationTests/GetSomeEntityCountQueryHandlerTests.cs` | Query handler behaviour. |
| `modulith/test/.../Orders.ApplicationTests/AddSomeEntityCommandHandlerTests.cs` | Command handler behaviour. |
| `feature/.../FeatureName.Application/{Commands,Queries,Dtos}/.gitkeep` | Scaffolded folder hints. |

**Modified**

| File | Change |
|---|---|
| `modulith/Directory.Packages.props` | Three new `PackageVersion` entries. |
| `modulith/ModulithTemplate.slnx` | Register `test/ModulithTemplate.WebTests`. |
| `modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj` | Mediator + FluentResults references, `InternalsVisibleTo`. |
| `modulith/src/ModulithTemplate.Web/Program.cs` | `AddMediator(…)`. |
| `modulith/src/ModulithTemplate.Web.Common/Extensions/ServiceScopeExtensions.cs` | Two `ValueTask` overloads. |
| `modulith/src/.../Orders.Application/…Application.csproj` | Mediator + FluentResults references, `InternalsVisibleTo`. |
| `modulith/src/.../Orders.Web/…Web.csproj` | `Mediator.Abstractions`. |
| `modulith/test/…ArchitectureTests/NamingConventionTests.cs` | Multi-suffix conventions; drop app service; add command/query/dto. |
| `modulith/CLAUDE.md` | Architecture, pipeline subsection, Blazor snippet, conventions. |
| `feature/.../FeatureName.Application/…Application.csproj` | Mediator + FluentResults, `InternalsVisibleTo`. |
| `feature/.../FeatureName.Web/…Web.csproj` | `Mediator.Abstractions`. |

**Deleted**

- `modulith/src/.../Orders.Application/Commands/.gitkeep` and `Queries/.gitkeep` — those folders hold real files after Task 4.

---

### Task 1: Exception behaviour and the host test project

Creates the host test project first, because it is the only place the behaviours can be tested from, then delivers `ExceptionBehaviour` under test. `AddMediator` is not wired yet — the behaviour is a plain class and is tested by calling `Handle` directly with a stub `next` delegate.

**Files:**
- Modify: `working/content/modulith/Directory.Packages.props`
- Modify: `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj`
- Modify: `working/content/modulith/ModulithTemplate.slnx`
- Create: `working/content/modulith/test/ModulithTemplate.WebTests/ModulithTemplate.WebTests.csproj`
- Create: `working/content/modulith/src/ModulithTemplate.Web/Behaviours/BehaviourLog.cs`
- Create: `working/content/modulith/src/ModulithTemplate.Web/Behaviours/ExceptionBehaviour.cs`
- Test: `working/content/modulith/test/ModulithTemplate.WebTests/ExceptionBehaviourTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `ModulithTemplate.Web.Behaviours.ExceptionBehaviour<TMessage, TResponse>` — `internal sealed`, primary constructor takes `ILogger<ExceptionBehaviour<TMessage, TResponse>>`, constrained `where TMessage : IMessage` and `where TResponse : ResultBase<TResponse>, new()`.
  - `ModulithTemplate.Web.Behaviours.BehaviourLog` — `internal static partial class` with `Handling(ILogger, string)`, `Handled(ILogger, string, long)`, `Failed(ILogger, string, long, string)`, `HandlerThrew(ILogger, string, Exception)`.

- [ ] **Step 1: Pin the new packages**

In `working/content/modulith/Directory.Packages.props`, add three `PackageVersion` entries to the existing `ItemGroup`, keeping the list alphabetically ordered (so they sit after `Meziantou.Analyzer` / before `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, and after `Microsoft.EntityFrameworkCore.Design` respectively):

```xml
<PackageVersion Include="Mediator.Abstractions" Version="3.0.2" />
<PackageVersion Include="Mediator.SourceGenerator" Version="3.0.2" />
<PackageVersion Include="Microsoft.Extensions.Diagnostics.Testing" Version="10.8.0" />
```

`Microsoft.Extensions.Diagnostics.Testing` follows the `dotnet/extensions` version line (10.8.0), which is unrelated to the 10.0.10 runtime line used by the `Microsoft.Extensions.*` pins directly above it. Do not "correct" it to 10.0.10.

**If restore fails with NU1109**, `CentralPackageTransitivePinningEnabled` has caught a pin sitting below what one of these packages transitively requires — most likely a `Microsoft.Extensions.*` runtime-line pin that `Microsoft.Extensions.Diagnostics.Testing` 10.8.0 needs newer. Raise the named pin to the version the error demands; never switch off transitive pinning to make it go away.

- [ ] **Step 2: Reference the packages from the host and open it to its test project**

Replace the two `ItemGroup`s in `working/content/modulith/src/ModulithTemplate.Web/ModulithTemplate.Web.csproj` with:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Mediator.Abstractions" />
    <PackageReference Include="Mediator.SourceGenerator" PrivateAssets="all" />
    <PackageReference Include="FluentResults" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ModulithTemplate.WebTests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../ModulithTemplate.Web.Common/ModulithTemplate.Web.Common.csproj" />
    <ProjectReference Include="../Features/Orders/ModulithTemplate.Features.Orders.Web/ModulithTemplate.Features.Orders.Web.csproj" />
  </ItemGroup>
```

Leave the existing `PropertyGroup` with `BlazorDisableThrowNavigationException` untouched. `InternalsVisibleTo` is an MSBuild item the SDK turns into the assembly attribute; the `ModulithTemplate` token inside it is renamed at scaffold time along with everything else.

- [ ] **Step 3: Create the host test project**

Create `working/content/modulith/test/ModulithTemplate.WebTests/ModulithTemplate.WebTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../src/ModulithTemplate.Web/ModulithTemplate.Web.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Diagnostics.Testing" />
  </ItemGroup>

</Project>
```

Target framework, analyzers, xUnit, NSubstitute, and the runner come from `test/Directory.Build.props`. The extra `PackageReference` is a deliberate exception to the "a test csproj holds nothing but a `ProjectReference`" norm — no other test project needs `FakeLogger`.

- [ ] **Step 4: Register the test project in the solution**

In `working/content/modulith/ModulithTemplate.slnx`, add the project to the existing `/test/` folder, before the architecture tests entry so the folder stays alphabetical:

```xml
  <Folder Name="/test/">
    <Project Path="test/ModulithTemplate.ArchitectureTests/ModulithTemplate.ArchitectureTests.csproj" />
    <Project Path="test/ModulithTemplate.WebTests/ModulithTemplate.WebTests.csproj" />
  </Folder>
```

- [ ] **Step 5: Write the failing tests**

Create `working/content/modulith/test/ModulithTemplate.WebTests/ExceptionBehaviourTests.cs`:

```csharp
using FluentResults;

using Mediator;

using Microsoft.Extensions.Logging.Abstractions;

using ModulithTemplate.Web.Behaviours;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="ExceptionBehaviour{TMessage, TResponse}"/>.</summary>
public class ExceptionBehaviourTests
{
    /// <summary>Stand-in message type; the behaviour never inspects the message itself.</summary>
    public sealed record TestQuery : IQuery<Result<int>>;

    /// <summary>Stand-in message type for the non-generic <see cref="Result"/> case.</summary>
    public sealed record TestCommand : ICommand<Result>;

    [Fact]
    public async Task Handle_when_the_handler_throws_returns_a_failed_result_carrying_the_exception()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestQuery, Result<int>>(NullLogger<ExceptionBehaviour<TestQuery, Result<int>>>.Instance);
        var boom = new InvalidOperationException("boom");

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => throw boom, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailed);
        var error = Assert.Single(result.Errors);
        Assert.Same(boom, Assert.IsType<ExceptionalError>(error).Exception);
    }

    [Fact]
    public async Task Handle_when_the_handler_throws_returns_a_failed_result_for_a_non_generic_result_response()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestCommand, Result>(NullLogger<ExceptionBehaviour<TestCommand, Result>>.Instance);

        // Act
        var result = await behaviour.Handle(new TestCommand(), (_, _) => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_when_the_handler_is_cancelled_rethrows_instead_of_converting()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestQuery, Result<int>>(NullLogger<ExceptionBehaviour<TestQuery, Result<int>>>.Instance);

        // Act
        var act = async () => await behaviour.Handle(new TestQuery(), (_, _) => throw new OperationCanceledException(), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task Handle_when_the_handler_succeeds_passes_the_result_through_untouched()
    {
        // Arrange
        var behaviour = new ExceptionBehaviour<TestQuery, Result<int>>(NullLogger<ExceptionBehaviour<TestQuery, Result<int>>>.Instance);
        var expected = Result.Ok(42);

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(expected), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
    }
}
```

`NullLogger<T>.Instance` comes from the shared framework, so no extra package is needed here; the log-level assertions live in Task 2 where `FakeLogger` is introduced.

- [ ] **Step 6: Run the tests to verify they fail**

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.WebTests
```

Expected: compile failure — `ExceptionBehaviour` and `BehaviourLog` do not exist yet.

- [ ] **Step 7: Write the log message class**

Create `working/content/modulith/src/ModulithTemplate.Web/Behaviours/BehaviourLog.cs`:

```csharp
namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Source-generated log messages for the mediator pipeline behaviours.
/// </summary>
/// <remarks>
/// Kept non-generic and separate from the behaviours themselves: the <c>[LoggerMessage]</c>
/// generator does not support generic containing types, and both behaviours are open generics.
/// Each method therefore takes the <see cref="ILogger"/> as its first parameter.
/// </remarks>
internal static partial class BehaviourLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Handling {MessageType}")]
    public static partial void Handling(ILogger logger, string messageType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Handled {MessageType} in {ElapsedMilliseconds} ms")]
    public static partial void Handled(ILogger logger, string messageType, long elapsedMilliseconds);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "{MessageType} failed in {ElapsedMilliseconds} ms: {Errors}")]
    public static partial void Failed(ILogger logger, string messageType, long elapsedMilliseconds, string errors);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "{MessageType} threw an unhandled exception")]
    public static partial void HandlerThrew(ILogger logger, string messageType, Exception exception);
}
```

`ILogger`, `LogLevel`, and `LoggerMessageAttribute` resolve through the host's implicit usings plus the ASP.NET Core shared framework. If the build reports them as unresolved, add `using Microsoft.Extensions.Logging;` at the top rather than adding a package.

- [ ] **Step 8: Write the exception behaviour**

Create `working/content/modulith/src/ModulithTemplate.Web/Behaviours/ExceptionBehaviour.cs`:

```csharp
using FluentResults;

using Mediator;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Converts an unhandled exception thrown by a message handler into a failed result of the
/// handler's own response type, so callers branch on <c>IsFailed</c> instead of catching.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// The <typeparamref name="TResponse"/> constraint is what makes the conversion type-safe:
/// FluentResults parameterises each result type by itself (<c>Result : ResultBase&lt;Result&gt;</c>,
/// <c>Result&lt;T&gt; : ResultBase&lt;Result&lt;T&gt;&gt;</c>), so <c>WithError</c> returns the
/// concrete type it was called on. It also decides which messages this behaviour applies to at
/// all — a handler returning something other than a result is not wrapped, and its exceptions
/// propagate. That is why every handler in this solution returns <c>Result</c> or
/// <c>Result&lt;T&gt;</c>.
/// </remarks>
internal sealed class ExceptionBehaviour<TMessage, TResponse>(
    ILogger<ExceptionBehaviour<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : ResultBase<TResponse>, new()
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a failure — let the caller observe it as cancellation.
            throw;
        }
        catch (Exception ex)
        {
            BehaviourLog.HandlerThrew(logger, typeof(TMessage).Name, ex);
            return new TResponse().WithError(new ExceptionalError(ex));
        }
    }
}
```

- [ ] **Step 9: Run the tests to verify they pass**

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.WebTests
```

Expected: 4 passed.

- [ ] **Step 10: Build warning-clean and suppress narrowly if needed**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror
```

Catching `Exception` is the one construct here likely to trip an analyzer — SonarAnalyzer's **S2221** (`"Exception" should not be caught`) and, if enabled, **CA1031**. If either fires, wrap only the general catch:

```csharp
#pragma warning disable S2221 // "Exception" should not be caught — converting any handler failure into a Result is this behaviour's entire purpose
        catch (Exception ex)
#pragma warning restore S2221
        {
```

Do not disable the rule solution-wide, and do not add a suppression for a warning that did not actually appear — with `EnforceCodeStyleInBuild`, an unnecessary suppression can itself be reported as IDE0079.

- [ ] **Step 11: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, every test green. Do not commit — the maintainer handles git.

---

### Task 2: Logging behaviour

**Files:**
- Create: `working/content/modulith/src/ModulithTemplate.Web/Behaviours/LoggingBehaviour.cs`
- Test: `working/content/modulith/test/ModulithTemplate.WebTests/LoggingBehaviourTests.cs`

**Interfaces:**
- Consumes: `BehaviourLog.Handling/Handled/Failed` from Task 1.
- Produces: `ModulithTemplate.Web.Behaviours.LoggingBehaviour<TMessage, TResponse>` — `internal sealed`, primary constructor takes `ILogger<LoggingBehaviour<TMessage, TResponse>>`, constrained `where TMessage : IMessage` only.

- [ ] **Step 1: Write the failing tests**

Create `working/content/modulith/test/ModulithTemplate.WebTests/LoggingBehaviourTests.cs`:

```csharp
using FluentResults;

using Mediator;

using Microsoft.Extensions.Diagnostics.Testing;
using Microsoft.Extensions.Logging;

using ModulithTemplate.Web.Behaviours;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for <see cref="LoggingBehaviour{TMessage, TResponse}"/>.</summary>
public class LoggingBehaviourTests
{
    /// <summary>Stand-in message type; the behaviour never inspects the message itself.</summary>
    public sealed record TestQuery : IQuery<Result<int>>;

    [Fact]
    public async Task Handle_when_the_handler_succeeds_logs_the_outcome_at_information()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Ok(1)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Information);
        Assert.DoesNotContain(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Handle_when_the_handler_returns_a_failed_result_logs_the_outcome_at_warning()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);

        // Act
        await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(Result.Fail<int>("nope")), TestContext.Current.CancellationToken);

        // Assert
        var warning = Assert.Single(logger.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
        Assert.Contains("nope", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_when_the_handler_succeeds_passes_the_result_through_untouched()
    {
        // Arrange
        var logger = new FakeLogger<LoggingBehaviour<TestQuery, Result<int>>>();
        var behaviour = new LoggingBehaviour<TestQuery, Result<int>>(logger);
        var expected = Result.Ok(7);

        // Act
        var result = await behaviour.Handle(new TestQuery(), (_, _) => ValueTask.FromResult(expected), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.WebTests --filter-class "*LoggingBehaviourTests*"
```

Expected: compile failure — `LoggingBehaviour` does not exist.

- [ ] **Step 3: Write the logging behaviour**

Create `working/content/modulith/src/ModulithTemplate.Web/Behaviours/LoggingBehaviour.cs`:

```csharp
using System.Diagnostics;

using FluentResults;

using Mediator;

namespace ModulithTemplate.Web.Behaviours;

/// <summary>
/// Times every message passing through the mediator and logs its outcome.
/// </summary>
/// <typeparam name="TMessage">The message being handled.</typeparam>
/// <typeparam name="TResponse">The handler's response type.</typeparam>
/// <remarks>
/// Deliberately unconstrained on <typeparamref name="TResponse"/>, so that every message is
/// logged regardless of what its handler returns — logging must never be conditional on the
/// shape of a response. Responses that happen to be results are inspected at runtime so a
/// failed result is reported at warning rather than information.
/// </remarks>
internal sealed class LoggingBehaviour<TMessage, TResponse>(
    ILogger<LoggingBehaviour<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage).Name;
        BehaviourLog.Handling(logger, messageType);

        var startedAt = Stopwatch.GetTimestamp();
        var response = await next(message, cancellationToken);
        var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (response is IResultBase { IsFailed: true } failed)
        {
            BehaviourLog.Failed(logger, messageType, elapsedMilliseconds, string.Join("; ", failed.Errors.Select(error => error.Message)));
        }
        else
        {
            BehaviourLog.Handled(logger, messageType, elapsedMilliseconds);
        }

        return response;
    }
}
```

`IResultBase.Errors` is an `IReadOnlyList<IError>` on the FluentResults interface, so no cast to a concrete result type is needed.

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.WebTests
```

Expected: 7 passed (4 from Task 1, 3 new).

- [ ] **Step 5: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, all tests green.

---

### Task 3: `ValueTask` overloads on `ServiceScopeExtensions`

`IMediator.Send` returns `ValueTask<T>`, but the existing helper only accepts `Func<IServiceProvider, Task<T>>`, which would force a trailing `.AsTask()` at every Blazor call site.

**Files:**
- Modify: `working/content/modulith/src/ModulithTemplate.Web.Common/Extensions/ServiceScopeExtensions.cs`
- Test: `working/content/modulith/test/ModulithTemplate.WebTests/ServiceScopeExtensionsTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `ServiceScopeExtensions.WithNewScopeAsync(this IServiceScopeFactory, Func<IServiceProvider, ValueTask>)` returning `ValueTask`, and `WithNewScopeAsync<TResult>(this IServiceScopeFactory, Func<IServiceProvider, ValueTask<TResult>>)` returning `ValueTask<TResult>`.

- [ ] **Step 1: Write the failing tests**

Create `working/content/modulith/test/ModulithTemplate.WebTests/ServiceScopeExtensionsTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Web.Common.Extensions;

namespace ModulithTemplate.WebTests;

/// <summary>Tests for the <c>ValueTask</c> overloads of <see cref="ServiceScopeExtensions"/>.</summary>
public class ServiceScopeExtensionsTests
{
    /// <summary>Scoped probe recording whether its scope was disposed.</summary>
    public sealed class ScopedProbe : IDisposable
    {
        /// <summary>Whether <see cref="Dispose"/> has run.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task WithNewScopeAsync_with_a_value_task_action_returns_its_value_and_disposes_the_scope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        ScopedProbe? captured = null;

        // Act
        var result = await scopeFactory.WithNewScopeAsync(sp =>
        {
            captured = sp.GetRequiredService<ScopedProbe>();
            return ValueTask.FromResult(42);
        });

        // Assert
        Assert.Equal(42, result);
        Assert.True(captured!.Disposed);
    }

    [Fact]
    public async Task WithNewScopeAsync_with_a_value_task_action_that_returns_nothing_disposes_the_scope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        ScopedProbe? captured = null;

        // Act
        await scopeFactory.WithNewScopeAsync(sp =>
        {
            captured = sp.GetRequiredService<ScopedProbe>();
            return ValueTask.CompletedTask;
        });

        // Assert
        Assert.True(captured!.Disposed);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.WebTests --filter-class "*ServiceScopeExtensionsTests*"
```

Expected: compile failure — the lambdas return `ValueTask`, which no existing overload accepts.

- [ ] **Step 3: Add the overloads**

Append to the `ServiceScopeExtensions` class in `working/content/modulith/src/ModulithTemplate.Web.Common/Extensions/ServiceScopeExtensions.cs`, after the existing `Task<TResult>` overload:

```csharp
    /// <summary>
    /// Creates a new dependency injection scope, runs <paramref name="action"/> against the
    /// scope's <see cref="IServiceProvider"/>, and disposes the scope afterwards.
    /// </summary>
    /// <param name="scopeFactory">The factory used to create the scope.</param>
    /// <param name="action">The work to run with the scoped service provider.</param>
    /// <returns>A task that completes when the work and scope disposal have finished.</returns>
    /// <remarks>
    /// The <see cref="ValueTask"/> overloads exist so that a call returning a
    /// <see cref="ValueTask"/> — such as <c>IMediator.Send</c> — can be passed directly without
    /// a trailing <c>AsTask()</c> at every call site. The lambda's return type selects the
    /// overload, so these never compete with the <see cref="Task"/> ones.
    /// </remarks>
    public static async ValueTask WithNewScopeAsync(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopeFactory.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Creates a new dependency injection scope, runs <paramref name="action"/> against the
    /// scope's <see cref="IServiceProvider"/>, and disposes the scope afterwards.
    /// </summary>
    /// <typeparam name="TResult">The type of value produced by <paramref name="action"/>.</typeparam>
    /// <param name="scopeFactory">The factory used to create the scope.</param>
    /// <param name="action">The work to run with the scoped service provider.</param>
    /// <returns>A task producing the value returned by <paramref name="action"/>.</returns>
    public static async ValueTask<TResult> WithNewScopeAsync<TResult>(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, ValueTask<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(action);

        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.WebTests
```

Expected: 9 passed.

- [ ] **Step 5: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, all tests green.

---

### Task 4: Orders worked example

`SomeEntity` is mapped by nothing — `OrdersContext` declares no `DbSet<SomeEntity>` and `Data/Migrations/` holds only a `.gitkeep`. Adding a `DbSet` would make `build.yml`'s `has-pending-model-changes` check demand a committed migration, so the example deliberately stays compile-and-unit-test level, exactly like the existing smoke tests. Do not add a `DbSet` or a migration.

**Files:**
- Modify: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Application/ModulithTemplate.Features.Orders.Application.csproj`
- Modify: `working/content/modulith/src/Features/Orders/ModulithTemplate.Features.Orders.Web/ModulithTemplate.Features.Orders.Web.csproj`
- Create: `…Orders.Application/Queries/GetSomeEntityCount/GetSomeEntityCountQuery.cs`
- Create: `…Orders.Application/Queries/GetSomeEntityCount/GetSomeEntityCountQueryHandler.cs`
- Create: `…Orders.Application/Commands/AddSomeEntity/AddSomeEntityCommand.cs`
- Create: `…Orders.Application/Commands/AddSomeEntity/AddSomeEntityCommandHandler.cs`
- Create: `…Orders.Application/Dtos/.gitkeep`
- Delete: `…Orders.Application/Commands/.gitkeep`, `…Orders.Application/Queries/.gitkeep`
- Test: `…test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests/GetSomeEntityCountQueryHandlerTests.cs`
- Test: `…test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests/AddSomeEntityCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IOrdersRepository<T>` (existing, `ModulithTemplate.Features.Orders.Domain`), `SomeEntity` (existing, `…Domain.Entities`).
- Produces:
  - `GetSomeEntityCountQuery : IQuery<Result<int>>` in `ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount` — public, parameterless record.
  - `GetSomeEntityCountQueryHandler(IOrdersRepository<SomeEntity> repository)` — internal sealed, `IQueryHandler<GetSomeEntityCountQuery, Result<int>>`.
  - `AddSomeEntityCommand : ICommand<Result>` in `ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity` — public, parameterless record.
  - `AddSomeEntityCommandHandler(IOrdersRepository<SomeEntity> repository)` — internal sealed, `ICommandHandler<AddSomeEntityCommand, Result>`.

- [ ] **Step 1: Reference the packages and open the Application project to its test project**

Replace the first `ItemGroup` in `…Orders.Application/ModulithTemplate.Features.Orders.Application.csproj` and add an `InternalsVisibleTo` group, leaving the `ProjectReference` group untouched:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Mediator.Abstractions" />
    <PackageReference Include="FluentResults" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ModulithTemplate.Features.Orders.ApplicationTests" />
  </ItemGroup>
```

`InternalsVisibleTo` is required because handlers are `internal sealed` — without it the handler tests below cannot see the types they construct.

- [ ] **Step 2: Give the feature's Web project access to `IMediator`**

Add to the first `ItemGroup` of `…Orders.Web/ModulithTemplate.Features.Orders.Web.csproj`, so the feature's first Blazor component can inject `IMediator` without a csproj edit:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Mediator.Abstractions" />
  </ItemGroup>
```

- [ ] **Step 3: Write the failing handler tests**

Create `…test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests/GetSomeEntityCountQueryHandlerTests.cs`:

```csharp
using ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Tests for <see cref="GetSomeEntityCountQueryHandler"/>.</summary>
public class GetSomeEntityCountQueryHandlerTests
{
    [Fact]
    public async Task Handle_with_entities_in_the_repository_returns_a_successful_result_carrying_the_count()
    {
        // Arrange
        var repository = Substitute.For<IOrdersRepository<SomeEntity>>();
        repository.CountAsync(Arg.Any<CancellationToken>()).Returns(3);
        var handler = new GetSomeEntityCountQueryHandler(repository);

        // Act
        var result = await handler.Handle(new GetSomeEntityCountQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
    }
}
```

Create `…test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests/AddSomeEntityCommandHandlerTests.cs`:

```csharp
using ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;
using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

using NSubstitute;

namespace ModulithTemplate.Features.Orders.ApplicationTests;

/// <summary>Tests for <see cref="AddSomeEntityCommandHandler"/>.</summary>
public class AddSomeEntityCommandHandlerTests
{
    [Fact]
    public async Task Handle_adds_the_entity_to_the_repository_and_returns_a_successful_result()
    {
        // Arrange
        var repository = Substitute.For<IOrdersRepository<SomeEntity>>();
        var handler = new AddSomeEntityCommandHandler(repository);

        // Act
        var result = await handler.Handle(new AddSomeEntityCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        await repository.Received(1).AddAsync(Arg.Any<SomeEntity>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
cd working/content/modulith && dotnet test --project test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests
```

Expected: compile failure — the query, command, and handlers do not exist.

- [ ] **Step 5: Write the query and its handler**

Create `…Orders.Application/Queries/GetSomeEntityCount/GetSomeEntityCountQuery.cs`:

```csharp
using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;

/// <summary>
/// Counts the entities owned by the Orders feature. Replace this with a real query once the
/// feature has one — it exists to demonstrate the query shape, not to be useful.
/// </summary>
public sealed record GetSomeEntityCountQuery : IQuery<Result<int>>;
```

Create `…Orders.Application/Queries/GetSomeEntityCount/GetSomeEntityCountQueryHandler.cs`:

```csharp
using FluentResults;

using Mediator;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Application.Queries.GetSomeEntityCount;

/// <summary>Handles <see cref="GetSomeEntityCountQuery"/>.</summary>
/// <param name="repository">The Orders feature's repository.</param>
internal sealed class GetSomeEntityCountQueryHandler(IOrdersRepository<SomeEntity> repository)
    : IQueryHandler<GetSomeEntityCountQuery, Result<int>>
{
    /// <inheritdoc />
    public async ValueTask<Result<int>> Handle(GetSomeEntityCountQuery query, CancellationToken cancellationToken)
    {
        var count = await repository.CountAsync(cancellationToken);
        return Result.Ok(count);
    }
}
```

Note there is no `try`/`catch`: unhandled exceptions are converted to a failed `Result` by `ExceptionBehaviour`. Handlers hold the happy path plus explicit domain failures only.

- [ ] **Step 6: Write the command and its handler**

Create `…Orders.Application/Commands/AddSomeEntity/AddSomeEntityCommand.cs`:

```csharp
using FluentResults;

using Mediator;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>
/// Adds an entity to the Orders feature. Replace this with a real command once the feature has
/// one — it exists to demonstrate the command shape, including a non-generic
/// <see cref="Result"/> response.
/// </summary>
public sealed record AddSomeEntityCommand : ICommand<Result>;
```

Create `…Orders.Application/Commands/AddSomeEntity/AddSomeEntityCommandHandler.cs`:

```csharp
using FluentResults;

using Mediator;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>Handles <see cref="AddSomeEntityCommand"/>.</summary>
/// <param name="repository">The Orders feature's repository.</param>
internal sealed class AddSomeEntityCommandHandler(IOrdersRepository<SomeEntity> repository)
    : ICommandHandler<AddSomeEntityCommand, Result>
{
    /// <inheritdoc />
    public async ValueTask<Result> Handle(AddSomeEntityCommand command, CancellationToken cancellationToken)
    {
        await repository.AddAsync(new SomeEntity(), cancellationToken);
        return Result.Ok();
    }
}
```

- [ ] **Step 7: Tidy the folder placeholders**

Delete `…Orders.Application/Commands/.gitkeep` and `…Orders.Application/Queries/.gitkeep` — both folders now hold real files. Create an empty `…Orders.Application/Dtos/.gitkeep` so the DTO folder the naming rules expect exists as a hint.

- [ ] **Step 8: Update the stale smoke-test comment**

In `…Orders.ApplicationTests/OrdersApplicationSmokeTests.cs`, the XML comment on the single test ends with "Replace this with a real app-service test once the layer registers one." App services no longer exist in this architecture. Change that sentence to:

```
/// Handler behaviour is covered by the per-handler tests alongside this file; this one only
/// guards the layer's DI composition.
```

- [ ] **Step 9: Run the tests to verify they pass**

```bash
cd working/content/modulith && dotnet test --project test/Features/Orders/ModulithTemplate.Features.Orders.ApplicationTests
```

Expected: 3 passed (the existing smoke test plus the two new handler tests).

- [ ] **Step 10: Build warning-clean**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror
```

If SonarAnalyzer flags either parameterless record as an empty type (**S2094**, the rule already suppressed around `SomeEntity` in the Domain project), wrap just that declaration:

```csharp
#pragma warning disable S2094 // Classes should not be empty — a parameterless message carries meaning through its type
public sealed record AddSomeEntityCommand : ICommand<Result>;
#pragma warning restore S2094
```

Apply it only to the declarations that actually warned.

- [ ] **Step 11: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, all tests green.

---

### Task 5: Wire the mediator into the host

Deliberately sequenced after the worked example so the source generator has real messages to bind, which is what makes the generated-code inspection in Step 4 meaningful. This is where the spec's two open risks get resolved.

**Files:**
- Modify: `working/content/modulith/src/ModulithTemplate.Web/Program.cs`

**Interfaces:**
- Consumes: `LoggingBehaviour<,>` and `ExceptionBehaviour<,>` (Tasks 1–2); `GetSomeEntityCountQuery`, `AddSomeEntityCommand` and their handlers (Task 4).
- Produces: a configured `IMediator` in the host container, `Scoped`, wrapped by both behaviours.

- [ ] **Step 1: Register the mediator**

In `working/content/modulith/src/ModulithTemplate.Web/Program.cs`, add the using directives and the registration. The result of the top of the file:

```csharp
using Mediator;

using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Web;
using ModulithTemplate.Web.Behaviours;
using ModulithTemplate.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureOrdersFeature();

builder.Services.AddMediator(options =>
{
    // Scoped, not the library default of Singleton: handlers inject the feature repositories,
    // which are bound to a scoped DbContext. This also makes IMediator itself scoped, which is
    // why components resolve it inside WithNewScopeAsync rather than from the root provider.
    options.ServiceLifetime = ServiceLifetime.Scoped;

    // Ordered outermost-first. LoggingBehaviour therefore observes a uniform Result outcome for
    // every message, because ExceptionBehaviour has already converted any exception below it.
    options.PipelineBehaviors = [typeof(LoggingBehaviour<,>), typeof(ExceptionBehaviour<,>)];
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
```

Leave the rest of the file, including the `S6966` pragma around `app.Run()`, exactly as it is. If `ServiceLifetime` resolves without the explicit `using Microsoft.Extensions.DependencyInjection;`, drop that line rather than leaving an unused using — it would be reported under `-warnaserror`.

- [ ] **Step 2: Build**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror
```

Expected: clean.

**Risk 1 — analyzers over generated code.** `Mediator.g.cs` is compiled into the host, where Meziantou, SonarAnalyzer, and Roslynator all run. If the build fails with warnings whose file path points into `obj/**/generated/**`, add to `working/content/modulith/.editorconfig` (creating the file if absent):

```ini
[*.g.cs]
generated_code = true
```

Do not silence the rules globally and do not edit generated output.

- [ ] **Step 3: Verify the handlers were discovered**

```bash
cd working/content/modulith && dotnet build src/ModulithTemplate.Web -p:EmitCompilerGeneratedFiles=true && \
  grep -rn "GetSomeEntityCountQueryHandler\|AddSomeEntityCommandHandler" src/ModulithTemplate.Web/obj/
```

Expected: both handler type names appear in the generated mediator registrations. This proves discovery reaches a feature's `Application` assembly through the host → `Orders.Web` → `Orders.Application` reference chain, with no per-feature entry in `Program.cs`.

- [ ] **Step 4: Verify the behaviours wrap the handlers**

```bash
cd working/content/modulith && grep -rn "LoggingBehaviour\|ExceptionBehaviour" src/ModulithTemplate.Web/obj/
```

Expected: both behaviours appear in the generated pipeline for the messages returning `Result`/`Result<T>`.

**Risk 2 — the `TResponse` constraint.** If the build fails because the generator cannot satisfy `ExceptionBehaviour`'s constraint, or if `ExceptionBehaviour` is absent from the generated pipeline while `LoggingBehaviour` is present, the generator is not honouring a `TResponse` constraint when selecting behaviours. Do **not** weaken the behaviour. Instead switch it to the README's other documented registration path, where the DI container performs the open-generic constraint match at resolution time — remove it from the options array and register it directly:

```csharp
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(LoggingBehaviour<,>)];
});

builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ExceptionBehaviour<,>));
```

Re-run Step 4 to confirm the behaviour now applies. If this fallback is taken, record it in the spec's "Risks to verify" section and note in `CLAUDE.md` (Task 8, Step 2) that the two behaviours are registered by different mechanisms, since that is otherwise surprising to a reader.

- [ ] **Step 5: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, all tests green.

---

### Task 6: Architecture naming conventions

**Files:**
- Modify: `working/content/modulith/test/ModulithTemplate.ArchitectureTests/NamingConventionTests.cs`

**Interfaces:**
- Consumes: the Task 4 example types, which the new rules must accept.
- Produces: `NamingConvention(string Name, string NamespaceSuffix, string[] TypeSuffixes)` and `TypeNamePattern(string[] typeSuffixes)`.

- [ ] **Step 1: Prove the new rules would catch a violation**

Temporarily create `…Orders.Application/Commands/AddSomeEntity/Broken.cs`:

```csharp
namespace ModulithTemplate.Features.Orders.Application.Commands.AddSomeEntity;

/// <summary>Temporary violation used to prove the naming rule bites. Delete in Step 5.</summary>
public sealed class Broken;
```

- [ ] **Step 2: Update the conventions table and the pattern helper**

In `NamingConventionTests.cs`, change the record and the convention list. The `app service` entry is removed — handlers replace app services, so a rule steering types into `Application.Services` would contradict the docs:

```csharp
    /// <summary>A building block, the namespace it belongs in, and the type-name suffixes it may carry.</summary>
    private sealed record NamingConvention(string Name, string NamespaceSuffix, string[] TypeSuffixes);

    private static readonly NamingConvention[] Conventions =
    [
        new("specification", "Domain.Specifications", ["Spec"]),
        new("mapper", "Application.Mappers", ["Mapper"]),
        new("domain service", "Domain.Services", ["DomainService"]),
        new("command", "Application.Commands", ["Command", "CommandHandler"]),
        new("query", "Application.Queries", ["Query", "QueryHandler"]),
        new("dto", "Application.Dtos", ["Dto"]),
    ];
```

Replace `TypeNamePattern` with the multi-suffix version, keeping its existing XML comment about the CLR arity suffix and appending the sentence below:

```csharp
    private static string TypeNamePattern(string[] typeSuffixes) =>
        ".*(" + string.Join("|", typeSuffixes.Select(Regex.Escape)) + @")(`\d+)?$";
```

Add to that method's XML remarks: *"A convention may carry more than one suffix — a `Commands` namespace legitimately holds both `*Command` and `*CommandHandler` types — so the suffixes are combined into an alternation."*

Both `AssertNaming` and `AssertPlacement` call `TypeNamePattern(convention.TypeSuffix)`; update both call sites to pass the collection instead:

```csharp
.Should().HaveNameMatching(TypeNamePattern(convention.TypeSuffixes))
```

```csharp
Types().That().HaveNameMatching(TypeNamePattern(convention.TypeSuffixes))
```

In the same two methods, the `Because(...)` strings interpolate `convention.TypeSuffix`; change them to render the set:

```csharp
            .Because($"every type in a feature's {convention.NamespaceSuffix} namespace is a {convention.Name} and must be named {string.Join(" or ", convention.TypeSuffixes.Select(suffix => "*" + suffix))}.")
```

```csharp
            .Because($"a {convention.Name} must live in its feature's {convention.NamespaceSuffix} namespace.")
```

(The placement message needs no suffix interpolation; only the naming one does.)

- [ ] **Step 3: Swap the facts**

Delete these two facts and their XML comments:

```csharp
    public void Types_in_Application_Services_end_with_AppService() => AssertNaming("app service");
    public void Types_named_AppService_reside_in_Application_Services() => AssertPlacement("app service");
```

Add six, each with an XML summary matching the style of the existing ones:

```csharp
    /// <summary>Every type in a feature's Commands namespace must be named <c>*Command</c> or <c>*CommandHandler</c>.</summary>
    [Fact]
    public void Types_in_Commands_end_with_Command_or_CommandHandler() => AssertNaming("command");

    /// <summary>Every type named <c>*Command</c> or <c>*CommandHandler</c> must reside in a feature's Commands namespace.</summary>
    [Fact]
    public void Types_named_Command_or_CommandHandler_reside_in_Commands() => AssertPlacement("command");

    /// <summary>Every type in a feature's Queries namespace must be named <c>*Query</c> or <c>*QueryHandler</c>.</summary>
    [Fact]
    public void Types_in_Queries_end_with_Query_or_QueryHandler() => AssertNaming("query");

    /// <summary>Every type named <c>*Query</c> or <c>*QueryHandler</c> must reside in a feature's Queries namespace.</summary>
    [Fact]
    public void Types_named_Query_or_QueryHandler_reside_in_Queries() => AssertPlacement("query");

    /// <summary>Every type in a feature's Dtos namespace must be named <c>*Dto</c>.</summary>
    [Fact]
    public void Types_in_Dtos_end_with_Dto() => AssertNaming("dto");

    /// <summary>Every type named <c>*Dto</c> must reside in a feature's Dtos namespace.</summary>
    [Fact]
    public void Types_named_Dto_reside_in_Dtos() => AssertPlacement("dto");
```

- [ ] **Step 4: Run the tests to verify the violation is caught**

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.ArchitectureTests --filter-class "*NamingConventionTests*"
```

Expected: `Types_in_Commands_end_with_Command_or_CommandHandler` FAILS, naming `Broken`. Every other fact passes. If it passes, the rule is not biting — check that the assembly was rebuilt and that `Broken` really landed under `…Application.Commands`.

- [ ] **Step 5: Delete the violation and re-run**

Delete `…Orders.Application/Commands/AddSomeEntity/Broken.cs`, then:

```bash
cd working/content/modulith && dotnet test --project test/ModulithTemplate.ArchitectureTests
```

Expected: all green, including the six new facts, which now match the Task 4 example types.

- [ ] **Step 6: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, all tests green.

---

### Task 7: Feature sub-template parity

Everything a scaffolded feature needs to write its first command without editing a `.csproj`. Mirrors Task 4's project changes into `working/content/feature/`.

**Files:**
- Modify: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj`
- Modify: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/ModulithApp.Features.FeatureName.Web.csproj`
- Create: `…FeatureName.Application/Commands/.gitkeep`, `…/Queries/.gitkeep`, `…/Dtos/.gitkeep`

**Interfaces:**
- Consumes: the package pins added in Task 1 (the sub-template's projects resolve them from the generated solution's `Directory.Packages.props`).
- Produces: no code — a scaffold whose `Application` project can declare `ICommand`/`IQuery` types and whose `Web` project can inject `IMediator` immediately.

- [ ] **Step 1: Update the Application project**

Replace the contents of `…FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Mediator.Abstractions" />
    <PackageReference Include="FluentResults" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ModulithApp.Features.FeatureName.ApplicationTests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj" />
  </ItemGroup>

</Project>
```

Both `ModulithApp` and `FeatureName` are replacement tokens, so the generated `InternalsVisibleTo` names the generated test project correctly.

- [ ] **Step 2: Update the Web project**

Replace the first `ItemGroup` of `…FeatureName.Web/ModulithApp.Features.FeatureName.Web.csproj`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Mediator.Abstractions" />
  </ItemGroup>
```

Leave the `ProjectReference` group untouched.

- [ ] **Step 3: Add the folder hints**

Create three empty files: `…FeatureName.Application/Commands/.gitkeep`, `…FeatureName.Application/Queries/.gitkeep`, `…FeatureName.Application/Dtos/.gitkeep`.

- [ ] **Step 4: Verify by scaffolding**

```bash
cd /workspaces/modulith-template
rm -rf /tmp/mediator-check && mkdir -p /tmp/mediator-check
dotnet new install working/content/modulith --force
dotnet new install working/content/feature --force
dotnet new modulith -n CheckApp -o /tmp/mediator-check/CheckApp
cd /tmp/mediator-check/CheckApp && dotnet new modulith-feature --appName CheckApp -n Payments
dotnet build CheckApp.slnx -warnaserror
```

Expected: the generated solution builds clean with nine feature projects wired in (Orders plus the new Payments layers and tests).

- [ ] **Step 5: Verify the scaffolded feature can actually send a command**

In the generated solution, create `src/Features/Payments/CheckApp.Features.Payments.Application/Commands/Ping/PingCommand.cs`:

```csharp
using FluentResults;

using Mediator;

namespace CheckApp.Features.Payments.Application.Commands.Ping;

public sealed record PingCommand : ICommand<Result>;
```

and `.../Commands/Ping/PingCommandHandler.cs`:

```csharp
using FluentResults;

using Mediator;

namespace CheckApp.Features.Payments.Application.Commands.Ping;

internal sealed class PingCommandHandler : ICommandHandler<PingCommand, Result>
{
    public ValueTask<Result> Handle(PingCommand command, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Ok());
}
```

Then wire the feature into the host as the generated `CLAUDE.md` documents and confirm discovery:

```bash
cd /tmp/mediator-check/CheckApp
dotnet add src/CheckApp.Web/CheckApp.Web.csproj reference \
  src/Features/Payments/CheckApp.Features.Payments.Web/CheckApp.Features.Payments.Web.csproj
# add `builder.ConfigurePaymentsFeature();` to src/CheckApp.Web/Program.cs
dotnet build src/CheckApp.Web -p:EmitCompilerGeneratedFiles=true
grep -rn "PingCommandHandler" src/CheckApp.Web/obj/
```

Expected: `PingCommandHandler` appears in the generated registrations — a newly scaffolded feature's handlers are discovered with no host change beyond the documented `ProjectReference` and `ConfigurePaymentsFeature()` call.

- [ ] **Step 6: Clean up**

```bash
cd /workspaces/modulith-template
dotnet new uninstall working/content/modulith
dotnet new uninstall working/content/feature
rm -rf /tmp/mediator-check
```

- [ ] **Step 7: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, all tests green.

---

### Task 8: Documentation

`working/content/modulith/CLAUDE.md` ships to generated projects — write it for the template's consumer, not for someone maintaining this repo.

**Files:**
- Modify: `working/content/modulith/CLAUDE.md`

**Interfaces:**
- Consumes: everything built in Tasks 1–7.
- Produces: no code.

- [ ] **Step 1: Rewrite the Application layer bullet**

In the Architecture section, replace the `<Name>.Application` bullet in full:

```markdown
- **`<Name>.Application`** — orchestration layer, entered only through the mediator. Each operation is a public `ICommand<T>`/`IQuery<T>` record plus an `internal sealed` handler, in a folder of its own: `Commands/PlaceOrder/{PlaceOrderCommand,PlaceOrderCommandHandler}.cs`, `Queries/GetOrderById/{GetOrderByIdQuery,GetOrderByIdQueryHandler}.cs`. Handlers load entities via repositories, invoke domain services, persist, and map to DTOs in `Dtos/`. They return **FluentResults** `Result`/`Result<T>` — always, because the exception behaviour is constrained to result responses and does not wrap a handler returning anything else. Handlers hold the happy path plus explicit `Result.Fail` for expected domain failures; they carry no `try`/`catch`, since unhandled exceptions are converted centrally (see "CQRS and the mediator pipeline"). Handlers being `internal` is what stops the `Web` layer reaching past `IMediator` to call one directly. Entity↔DTO mapping uses **Mapperly** source generators (`Mappers/*Mapper.cs`, `[Mapper]` partial classes).
```

- [ ] **Step 2: Add the pipeline subsection**

Insert immediately after the "Where business rules live" subsection (the outer fence below is four backticks so the nested C# block survives; insert the inner content, not the outer fence):

````markdown
### CQRS and the mediator pipeline

`Web` talks to `Application` through [martinothamar/Mediator](https://github.com/martinothamar/Mediator) — never by calling a handler or a service directly. The mediator's source generator runs only in the outermost executable, so `ModulithTemplate.Web/Program.cs` holds the single `AddMediator` call for the whole solution:

```csharp
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(LoggingBehaviour<,>), typeof(ExceptionBehaviour<,>)];
});
```

`ServiceLifetime.Scoped` is required rather than stylistic: handlers inject the feature repositories, which are bound to a scoped `DbContext`.

**Handlers register themselves.** The generated `AddMediator` discovers every handler in the assemblies the host references, so a feature's handlers reach the container without an entry in that feature's `Configuration.cs`. This is the one documented exception to "register services in the owning layer's `Configuration.cs`" — everything that is not a handler still belongs there.

Two behaviours wrap every message, outermost first:

- **`LoggingBehaviour`** — logs `Debug` on start and, on completion, `Information` with elapsed milliseconds, or `Warning` when the handler returned a failed `Result`.
- **`ExceptionBehaviour`** — catches unhandled exceptions, logs them at `Error` with the stack trace, and converts them into a failed `Result` of the handler's own response type. `OperationCanceledException` is rethrown untouched: cancellation is not a failure.

Both live in `ModulithTemplate.Web/Behaviours/` beside the registration that installs them, because the host is their only consumer.
````

- [ ] **Step 3: Update the Blazor access subsection**

In "Database access from Blazor components", replace the code sample and the sentence introducing it, so it reads (four-backtick outer fence again — insert the inner content only):

````markdown
Therefore **components must not `@inject` handlers or the mediator as a scoped dependency** for database work. Instead inject `IServiceScopeFactory` and send each message inside a fresh scope via the `WithNewScopeAsync` extension (`ModulithTemplate.Web.Common/Extensions/ServiceScopeExtensions.cs`, which has both `Task` and `ValueTask` overloads — `IMediator.Send` returns a `ValueTask`):

```csharp
var result = await ScopeFactory.WithNewScopeAsync(sp =>
    sp.GetRequiredService<IMediator>().Send(new PlaceOrderCommand(dto), CancellationToken.None));
```
````

Note the path correction: the current `CLAUDE.md` points at `ModulithTemplate.Web/Extensions/ServiceScopeExtensions.cs`, but the file actually lives in `ModulithTemplate.Web.Common/Extensions/`. Fix it while you are here.

- [ ] **Step 4: Extend the Conventions section**

Add a bullet after the existing **Tests** bullet:

```markdown
- **CQRS building blocks**: commands and their handlers live under `Application/Commands/<Operation>/` and are named `*Command` / `*CommandHandler`; queries under `Application/Queries/<Operation>/` as `*Query` / `*QueryHandler`; DTOs in `Application/Dtos/` as `*Dto`. `ModulithTemplate.ArchitectureTests` enforces all three in both directions, so a `*Dto` outside `Dtos/` fails just as a badly-named type inside it does.
```

- [ ] **Step 5: Mention the scaffolded folders**

In "Adding a feature", extend the `Payments.Application` line of the scaffold list:

```markdown
- `Payments.Application` — a `Configuration.cs` with an empty `ConfigurePaymentsApplication` to register non-handler services into, plus empty `Commands/`, `Queries/` and `Dtos/` folders. Handlers need no registration: the host's `AddMediator` discovers them.
```

- [ ] **Step 6: Check the docs against the code**

Re-read the edited sections against `Program.cs`, `Behaviours/`, and the Orders example. Every type name, folder, and method name mentioned must exist exactly as written. Confirm no reference to `I*AppService` or `Application/Services/` survives:

```bash
grep -n "AppService\|Application/Services" working/content/modulith/CLAUDE.md
```

Expected: no matches.

- [ ] **Step 7: Checkpoint**

```bash
cd working/content/modulith && dotnet build ModulithTemplate.slnx -warnaserror && dotnet test
```

Expected: build clean, all tests green.

---

### Task 9: End-to-end template verification

The template's real contract is what `dotnet new` produces, which no unit test covers.

**Files:** none modified — verification only.

**Interfaces:**
- Consumes: everything from Tasks 1–8.
- Produces: nothing.

- [ ] **Step 1: Build and test the content solution as CI does**

```bash
cd /workspaces/modulith-template
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror
cd working/content/modulith && dotnet test
```

Expected: clean build, all tests green.

- [ ] **Step 2: Scaffold both templates fresh**

```bash
cd /workspaces/modulith-template
rm -rf /tmp/modulith-verify && mkdir -p /tmp/modulith-verify
dotnet new install working/content/modulith --force
dotnet new install working/content/feature --force
dotnet new modulith -n VerifyApp -o /tmp/modulith-verify/VerifyApp
cd /tmp/modulith-verify/VerifyApp && dotnet new modulith-feature --appName VerifyApp -n Billing
dotnet build VerifyApp.slnx -warnaserror
dotnet test
```

Expected: clean build and green tests in the generated solution, including the generated `VerifyApp.WebTests` behaviour tests and the generated architecture tests.

- [ ] **Step 3: Confirm no template tokens survived**

```bash
cd /tmp/modulith-verify/VerifyApp
grep -rn "ModulithTemplate\|FeatureName\|ModulithApp" . --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git
```

Expected: no matches. A hit inside a `.csproj` most likely means an `InternalsVisibleTo` or `PackageReference` token was mistyped; a hit in a `.cs` file means a namespace or type name was.

- [ ] **Step 4: Confirm the pipeline is live at runtime**

```bash
cd /tmp/modulith-verify/VerifyApp/src/VerifyApp.Web && timeout 30 dotnet run 2>&1 | head -40
```

Expected: the host starts without a DI resolution error. A `ServiceLifetime` mismatch — the most likely wiring mistake — surfaces here as "Cannot consume scoped service … from singleton".

- [ ] **Step 5: Clean up**

```bash
cd /workspaces/modulith-template
dotnet new uninstall working/content/modulith
dotnet new uninstall working/content/feature
rm -rf /tmp/modulith-verify
```

- [ ] **Step 6: Report**

Summarise for the maintainer: which risks materialised (analyzers over generated code; the `TResponse` constraint), what the fallbacks were if any, and the final state of the working tree. Do not commit — the maintainer handles all git operations.

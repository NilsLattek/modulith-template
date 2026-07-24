# Feature Sub-Template (`modulith-feature`) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a second `dotnet new` template, `modulith-feature`, packed into the existing `Modulith` NuGet package, that scaffolds the four feature-layer projects (`Domain`/`Application`/`Infrastructure`/`Web`) and registers them in the host solution.

**Architecture:** A new template root `working/content/feature/` (sibling to `working/content/modulith/`), its own `.template.config/template.json` with `sourceName: "FeatureName"` and a required `appName` parameter symbol (`replaces`/`fileRename`: `ModulithApp`), and a well-known post-action (`D396686C-DE0E-4DE6-906D-291CD29FC5DE`) that adds the four generated `.csproj` files to the host `.slnx`. No packaging-project change is needed — `content/**` is already packed.

**Tech Stack:** .NET SDK 10.0.x, `dotnet new` template engine (`Microsoft.TemplateEngine.Tasks`), MSBuild SDK-style `.csproj`.

## Global Constraints

- `shortName: "modulith-feature"`, `sourceName: "FeatureName"`, `identity: "ModularMonolith.Template.Feature"`.
- `preferNameDirectory: false`, `tags.type: "item"`.
- Required parameter symbol `appName`: `datatype: string`, `isRequired: true`, `replaces: "ModulithApp"`, `fileRename: "ModulithApp"`.
- Generated project references (mirrors Orders minus its arch-test violation):
  - `<App>.Features.<Name>.Domain` — no references.
  - `<App>.Features.<Name>.Application` — references `Domain`.
  - `<App>.Features.<Name>.Infrastructure` — references `Domain`.
  - `<App>.Features.<Name>.Web` — references `Application` and `<App>.Web.Common` (via `../../../`).
- Each project gets exactly one `Placeholder.cs` with the `#pragma warning disable S2094` / `restore` empty-class suppression (same pattern as the existing `Class1.cs`/`ClassInfra.cs` in the Orders feature) so a scaffolded feature builds warning-clean under `-warnaserror`.
- One post-action, id `D396686C-DE0E-4DE6-906D-291CD29FC5DE` ("Add projects to solution"), `continueOnError: true`, `args.primaryOutputIndexes: "0;1;2;3"`.
- `primaryOutputs` lists the four `.csproj` paths, in the same order referenced by `primaryOutputIndexes` above.
- No packaging-csproj change: `working/ModularMonolith.Template.csproj` already packs `content/**` (excluding `bin`/`obj`), so `content/feature/` is picked up automatically.
- Non-goals (do not implement): host DI wiring into `Program.cs`, an `IntegrationEvents` project, a `--with-sample` rich slice.
- Do not perform any git actions (no `git add`, `git commit`, branches, pushes, or version bumps) — root `CLAUDE.md` reserves all git actions for the maintainer unless explicitly asked. Leave changes uncommitted in the working tree at the end of every task; the maintainer commits.

---

### Task 1: Create the `modulith-feature` template content and verify it end-to-end

**Files:**
- Create: `working/content/feature/.template.config/template.json`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Domain/Placeholder.cs`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/Placeholder.cs`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Infrastructure/ModulithApp.Features.FeatureName.Infrastructure.csproj`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Infrastructure/Placeholder.cs`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/ModulithApp.Features.FeatureName.Web.csproj`
- Create: `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/Placeholder.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks (this is the first task).
- Produces: the installable `modulith-feature` template at `working/content/feature/`, invoked as `dotnet new modulith-feature --appName <App> -n <Name>` from a generated solution's root. Task 2 (docs) and Task 3 (packed-nupkg verification) both reference this exact invocation and the `working/content/feature` path.

This project has no unit-test cycle of its own (it is template *content*, not compiled code from this repo) — its test cycle is the scaffold-build-test loop in Step 7 below, exactly as the design spec's "Testing strategy" section describes.

- [ ] **Step 1: Create the template.json**

Create `working/content/feature/.template.config/template.json`:

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Nils",
  "classifications": [
    "Modular Monolith",
    "Clean Architecture"
  ],
  "identity": "ModularMonolith.Template.Feature",
  "name": "Modular Monolith Feature",
  "shortName": "modulith-feature",
  "sourceName": "FeatureName",
  "preferNameDirectory": false,
  "tags": {
    "language": "C#",
    "type": "item"
  },
  "symbols": {
    "appName": {
      "type": "parameter",
      "datatype": "string",
      "isRequired": true,
      "replaces": "ModulithApp",
      "fileRename": "ModulithApp"
    }
  },
  "primaryOutputs": [
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj" },
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj" },
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Infrastructure/ModulithApp.Features.FeatureName.Infrastructure.csproj" },
    { "path": "src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/ModulithApp.Features.FeatureName.Web.csproj" }
  ],
  "postActions": [
    {
      "id": "addProjectsToSolution",
      "description": "Add projects to solution",
      "manualInstructions": [
        { "text": "Add the generated .csproj files to the solution manually with `dotnet sln add`" }
      ],
      "actionId": "D396686C-DE0E-4DE6-906D-291CD29FC5DE",
      "continueOnError": true,
      "args": {
        "primaryOutputIndexes": "0;1;2;3"
      }
    }
  ]
}
```

- [ ] **Step 2: Validate the JSON is well-formed**

Run: `python3 -m json.tool working/content/feature/.template.config/template.json`
Expected: pretty-printed JSON is echoed back with no `json.decoder.JSONDecodeError`.

- [ ] **Step 3: Create the Domain project (no references)**

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Domain/Placeholder.cs`:

```csharp
namespace ModulithApp.Features.FeatureName.Domain;

#pragma warning disable S2094 // Classes should not be empty
public class Placeholder
#pragma warning restore S2094 // Classes should not be empty
{

}
```

- [ ] **Step 4: Create the Application project (references Domain)**

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj" />
  </ItemGroup>

</Project>
```

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Application/Placeholder.cs`:

```csharp
namespace ModulithApp.Features.FeatureName.Application;

#pragma warning disable S2094 // Classes should not be empty
public class Placeholder
#pragma warning restore S2094 // Classes should not be empty
{

}
```

- [ ] **Step 5: Create the Infrastructure project (references Domain)**

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Infrastructure/ModulithApp.Features.FeatureName.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../ModulithApp.Features.FeatureName.Domain/ModulithApp.Features.FeatureName.Domain.csproj" />
  </ItemGroup>

</Project>
```

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Infrastructure/Placeholder.cs`:

```csharp
namespace ModulithApp.Features.FeatureName.Infrastructure;

#pragma warning disable S2094 // Classes should not be empty
public class Placeholder
#pragma warning restore S2094 // Classes should not be empty
{

}
```

- [ ] **Step 6: Create the Web project (references Application and `<App>.Web.Common`)**

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/ModulithApp.Features.FeatureName.Web.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../ModulithApp.Web.Common/ModulithApp.Web.Common.csproj" />
    <ProjectReference Include="../ModulithApp.Features.FeatureName.Application/ModulithApp.Features.FeatureName.Application.csproj" />
  </ItemGroup>

</Project>
```

Create `working/content/feature/src/Features/FeatureName/ModulithApp.Features.FeatureName.Web/Placeholder.cs`:

```csharp
namespace ModulithApp.Features.FeatureName.Web;

#pragma warning disable S2094 // Classes should not be empty
public class Placeholder
#pragma warning restore S2094 // Classes should not be empty
{

}
```

- [ ] **Step 7: Verify end-to-end by scaffolding a solution and a feature into it**

Run (from the repo root):

```bash
TEST_DIR=$(mktemp -d)
dotnet new install working/content/modulith
dotnet new install working/content/feature
dotnet new modulith -n MyApp -o "$TEST_DIR/MyApp"
cd "$TEST_DIR/MyApp"
dotnet new modulith-feature --appName MyApp -n Payments
echo "--- stray-token check ---"
grep -rl "FeatureName" src/Features/Payments && echo "FAIL: FeatureName leaked" || echo "OK: no FeatureName leak"
grep -rl "ModulithApp" src/Features/Payments && echo "FAIL: ModulithApp leaked" || echo "OK: no ModulithApp leak"
echo "--- solution registration check ---"
grep -A6 'Folder Name="/src/Features/Payments/"' MyApp.slnx
echo "--- build ---"
dotnet build MyApp.slnx -warnaserror
echo "--- test ---"
dotnet test MyApp.slnx
cd -
dotnet new uninstall working/content/modulith
dotnet new uninstall working/content/feature
rm -rf "$TEST_DIR"
```

Expected:
- Both stray-token checks print `OK:` (grep finds nothing, so the `&&` branch does not run).
- The `Folder Name="/src/Features/Payments/"` block lists all four `MyApp.Features.Payments.*.csproj` paths.
- `dotnet build ... -warnaserror` ends with `0 Warning(s)` and `0 Error(s)`.
- `dotnet test` reports all tests passed (the existing 5 architecture/unit tests — no new test files are added by this template, so the count should match a plain `dotnet new modulith` scaffold).

If any check fails, fix the relevant file from Steps 1–6 and re-run Step 7 before proceeding. Leave the working tree uncommitted — do not run `git add` or `git commit`; the maintainer commits.

---

### Task 2: Document the second template

**Files:**
- Modify: `CLAUDE.md` (repo root) — lines 39–48 (Commands section) and lines 51–55 (Template mechanics section)
- Modify: `working/content/modulith/CLAUDE.md` (ships to generated projects) — add an "Adding a feature" section
- Modify: `working/README.md` (packed package README)

**Interfaces:**
- Consumes: the `modulith-feature` template and its exact invocation from Task 1 (`dotnet new modulith-feature --appName <App> -n <Name>`, installed via `dotnet new install working/content/feature`).
- Produces: nothing consumed by later tasks (Task 3 verifies packaging independently of these doc files).

- [ ] **Step 1: Update the root CLAUDE.md Commands section**

In `/workspaces/modulith-template/CLAUDE.md`, replace the `## Commands` code block (currently lines 32–49) with:

```bash
# Build the template content exactly as CI does (must be warning-clean)
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror

# Test the template content
dotnet test working/content/modulith/ModulithTemplate.slnx

# Try the solution template locally: install from source, scaffold into a temp dir, then uninstall
dotnet new install working/content/modulith
dotnet new modulith -n MyApp -o /tmp/MyApp     # verify the ModulithTemplate rename worked everywhere
dotnet new uninstall working/content/modulith

# Try the feature sub-template locally: install from source, then scaffold a feature
# into an existing generated solution (run the `dotnet new modulith-feature` line from
# that solution's root, not from this repo)
dotnet new install working/content/feature
dotnet new modulith-feature --appName MyApp -n Payments
dotnet new uninstall working/content/feature

# Pack the NuGet package (output: working/bin/Release/Modulith.<version>.nupkg)
dotnet pack working/ModularMonolith.Template.csproj

# Test the packed .nupkg end-to-end (installs BOTH modulith and modulith-feature)
dotnet new install working/bin/Release/Modulith.*.nupkg
```

- [ ] **Step 2: Update the root CLAUDE.md Template mechanics section**

In `/workspaces/modulith-template/CLAUDE.md`, replace the `## Template mechanics` bullet list (currently lines 53–55) with:

```markdown
- **`sourceName` is `ModulithTemplate`** (`template.json`). The template engine replaces that string in **both file contents and file/folder paths** at scaffold time. So every namespace, project name, and directory in `working/content/modulith/` must keep the `ModulithTemplate` prefix — if you introduce an identifier that should be renamed per-project but omit the prefix, it will leak the template's name into generated projects. After any structural change, scaffold into a temp dir (see commands above) and confirm nothing named `ModulithTemplate` survives.
- **`shortName` is `modulith`** — the `dotnet new modulith` invocation name.
- The package project packs `content/**` (excluding `bin`/`obj`) and does not compile anything itself (`IncludeBuildOutput=false`, `Compile Remove="**\*"`).
- **A second template, `modulith-feature`**, lives alongside it at `working/content/feature/` (`sourceName: "FeatureName"`, a required `appName` parameter replacing the `ModulithApp` token). It scaffolds one feature's four layer projects (`Domain`/`Application`/`Infrastructure`/`Web`) into an *already-generated* solution and registers them in its `.slnx` via a post-action. Both templates pack into the single `Modulith` NuGet package — no extra install step is needed once a developer has installed `Modulith` to get `dotnet new modulith`. Verify changes to it the same way: scaffold a solution, scaffold a feature into it, build `-warnaserror`, and confirm nothing named `FeatureName` or `ModulithApp` survives.
```

- [ ] **Step 3: Add an "Adding a feature" section to the generated project's CLAUDE.md**

In `/workspaces/modulith-template/working/content/modulith/CLAUDE.md`, after the `## Docs` section (currently ending at line 109) and before `## Model Context Protocol (MCP) Servers`, insert:

```markdown
## Adding a feature

Scaffold a new feature's four layer projects and register them in the solution:

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Payments
```

Run this from the solution root (the directory containing the `.slnx`). It creates
`src/Features/Payments/ModulithTemplate.Features.Payments.{Domain,Application,Infrastructure,Web}`
and adds all four to the solution under a `/src/Features/Payments/` folder.

> **TODO:** the command does not wire the new feature's services into `Program.cs` /
> `Infrastructure` — there is no host module-registration convention yet. Follow the
> "Dependency injection" section above and register the feature's `Configuration.cs`
> manually until that convention exists.
```

Note: use the app's actual root namespace (the token that replaced `ModulithTemplate` at scaffold time) in place of `ModulithTemplate` in the `--appName` example above when this file ships inside a real generated project — leave it as `ModulithTemplate` in this source file, since the template engine's own `sourceName` substitution will rewrite it automatically when `working/content/modulith/` is scaffolded.

- [ ] **Step 4: Mention both templates in the packed package README**

In `/workspaces/modulith-template/working/README.md`, after the top-level `# README` heading (line 1) and before `## DB Migrations` (line 3), insert:

```markdown
## Templates in this package

- `dotnet new modulith` — scaffolds a new modular-monolith solution.
- `dotnet new modulith-feature --appName <App> -n <Name>` — run from a scaffolded solution's
  root to add one feature's four layer projects (`Domain`/`Application`/`Infrastructure`/`Web`),
  registered in the solution automatically.

```

- [ ] **Step 5: Leave the documentation updates uncommitted**

Do not run `git add` or `git commit` — leave `CLAUDE.md`, `working/content/modulith/CLAUDE.md`, and `working/README.md` as uncommitted changes in the working tree; the maintainer commits.

---

### Task 3: Verify the packed `.nupkg` carries both templates

**Files:**
- No file changes expected. This task only runs verification commands; if it uncovers a packaging gap, the fix belongs in `working/ModularMonolith.Template.csproj` (only if the `content/**` glob genuinely fails to pick up `content/feature/` — expected NOT to happen, since the glob is already recursive and excludes only `bin`/`obj`).

**Interfaces:**
- Consumes: `working/content/feature/` from Task 1, `working/ModularMonolith.Template.csproj` (existing, unchanged) from the repo's current state.
- Produces: nothing consumed by later tasks — this is the plan's final task.

This is the spec's documented "residual low-risk item": Task 1 verified the two templates installed **separately from source**; this task verifies they both come through a **single packed `.nupkg`**, matching how a real consumer installs the `Modulith` package.

- [ ] **Step 1: Pack the NuGet package**

Run: `dotnet pack working/ModularMonolith.Template.csproj`
Expected: build succeeds; output ends with something like
`working/bin/Release/Modulith.0.2.nupkg -> ...` (version matches the current `<PackageVersion>` in `working/ModularMonolith.Template.csproj`, unchanged by this task).

- [ ] **Step 2: Install the packed nupkg and confirm both templates are listed**

Run:

```bash
dotnet new install working/bin/Release/Modulith.*.nupkg
dotnet new list modulith
```

Expected: the `dotnet new list modulith` output includes two rows — one with short name `modulith` (name "Modular Monolith Solution") and one with short name `modulith-feature` (name "Modular Monolith Feature").

- [ ] **Step 3: Scaffold a solution and a feature entirely from the packed templates**

```bash
TEST_DIR=$(mktemp -d)
dotnet new modulith -n MyApp -o "$TEST_DIR/MyApp"
cd "$TEST_DIR/MyApp"
dotnet new modulith-feature --appName MyApp -n Billing
grep -rl "FeatureName" src/Features/Billing && echo "FAIL: FeatureName leaked" || echo "OK: no FeatureName leak"
dotnet build MyApp.slnx -warnaserror
cd -
rm -rf "$TEST_DIR"
```

Expected: `OK: no FeatureName leak`; build ends with `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 4: Uninstall the packed template package**

Run: `dotnet new uninstall Modulith`
Expected: confirms `Modulith` was uninstalled (both `modulith` and `modulith-feature` short names disappear from `dotnet new list`).

- [ ] **Step 5: No commit needed**

This task produces no tracked file changes (Step 1's `working/bin/` output is build output, not source — confirm it is covered by an existing `.gitignore` pattern such as `[Bb]in/` before finishing; if for any reason it is not ignored, do not `git add` it).

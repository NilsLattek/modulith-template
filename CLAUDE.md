# CLAUDE.md

## What this repository is

This repo is **not** an application — it is a `dotnet new` **template package** that produces one. It ships to nuget.org as the `Modulith` template; running `dotnet new modulith` scaffolds a modular-monolith solution from it.

There are two distinct concerns here, and it matters which one you're touching:

- **The template content** — `working/content/modulith/`. This is the actual solution source that gets copied (and token-renamed) into a user's new project. **Almost all real work happens here.** It has its own `CLAUDE.md` describing the VSA + DDD architecture of the generated app; **that file is guidance for the end user's generated project, not for maintaining this repo.** Read it to understand the code you're editing, but keep it written for the template's consumer.
- **The packaging** — `working/ModularMonolith.Template.csproj` (the NuGet template project) and `working/content/modulith/.template.config/template.json` (template metadata). These control how the content is packed and scaffolded.

This root `CLAUDE.md` is for developing the template itself.

## Layout

```
working/
  ModularMonolith.Template.csproj   # NuGet template package (PackageType=Template, PackageId=Modulith)
  README.md                          # packed as the package README
  content/
    modulith/                        # ← the template source (becomes the user's solution)
      .template.config/template.json # template metadata
      ModulithTemplate.slnx
      CLAUDE.md                      # ships to the generated project
      src/ ... test/ ...
```

The repo root also carries its own `.devcontainer`, `.claude`, `.github`, and `.editorconfig` for developing *this* repo; the content directory carries a parallel set that ships to generated projects. When changing devcontainer/CI/settings, be clear which layer you mean.

## Commands

```bash
# Build the template content (must be warning-clean, same semantics as CI).
# -v minimal cuts the per-project output-path noise; still prints warnings/errors.
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror -v minimal

# Test the template content. Must run from the content dir — global.json opts into
# Microsoft.Testing.Platform and is resolved from the current directory, so passing the .slnx as a
# path argument from the repo root gets forwarded to the test app instead of being treated as a
# target, which reports zero tests rather than failing.
cd working/content/modulith && dotnet test

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

## Template mechanics

- **`sourceName` is `ModulithTemplate`** (`template.json`). The template engine replaces that string in **both file contents and file/folder paths** at scaffold time. So every namespace, project name, and directory in `working/content/modulith/` must keep the `ModulithTemplate` prefix — if you introduce an identifier that should be renamed per-project but omit the prefix, it will leak the template's name into generated projects. After any structural change, scaffold into a temp dir (see commands above) and confirm nothing named `ModulithTemplate` survives.
- **`shortName` is `modulith`** — the `dotnet new modulith` invocation name.
- The package project packs `content/**` (excluding `bin`/`obj`) and does not compile anything itself (`IncludeBuildOutput=false`, `Compile Remove="**\*"`).
- **A second template, `modulith-feature`**, lives alongside it at `working/content/feature/` (`sourceName: "FeatureName"`, a required `appName` parameter replacing the `ModulithApp` token). It scaffolds one feature's layer projects (`Contracts`/`Domain`/`Application`/`Infrastructure`/`Web`) plus their matching test projects under `test/Features/<Name>/` into an *already-generated* solution and registers them in its `.slnx` via a post-action. Adding a layer project means editing `primaryOutputs` **and** the post-action's `primaryOutputIndexes` together; a mismatch scaffolds a project the solution never references, and only a real scaffold run reveals it. Both templates pack into the single `Modulith` NuGet package — no extra install step is needed once a developer has installed `Modulith` to get `dotnet new modulith`. Verify changes to it the same way: scaffold a solution, scaffold a feature into it, build `-warnaserror`, and confirm nothing named `FeatureName` or `ModulithApp` survives.

## Writing comments

**Keep them short.** An XML `<summary>` is a line or two. A `<remarks>` or an inline comment earns
its space only by recording what the code cannot say. Two tight lines beat a well-written paragraph; if a comment runs past a few lines, cut it rather than polishing it.

## Releasing

- Bump `<PackageVersion>` in `working/ModularMonolith.Template.csproj` before releasing.
- CI (`.github/workflows/build.yml`) builds the content solution with `-warnaserror` on every push/PR to `main` — keep it warning-clean.
- Publishing (`.github/workflows/publish.yml`) is triggered by a **published GitHub Release**: it builds Release, `dotnet pack`s, and pushes `Modulith.*.nupkg` to nuget.org using the `NUGET_APIKEY` secret. Cutting a GitHub Release is what ships a version.

## Versioning

Do not perform any git actions unless explicitly asked — the maintainer handles commits, branches, and releases.

## Additional Tools

@.claude/RTK.md


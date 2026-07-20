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
# Build the template content exactly as CI does (must be warning-clean)
dotnet build working/content/modulith/ModulithTemplate.slnx -warnaserror

# Test the template content
dotnet test working/content/modulith/ModulithTemplate.slnx

# Try the template locally: install from source, scaffold into a temp dir, then uninstall
dotnet new install working/content/modulith
dotnet new modulith -n MyApp -o /tmp/MyApp     # verify the ModulithTemplate rename worked everywhere
dotnet new uninstall working/content/modulith

# Pack the NuGet package (output: working/bin/Release/Modulith.<version>.nupkg)
dotnet pack working/ModularMonolith.Template.csproj

# Test the packed .nupkg end-to-end
dotnet new install working/bin/Release/Modulith.*.nupkg
```

## Template mechanics

- **`sourceName` is `ModulithTemplate`** (`template.json`). The template engine replaces that string in **both file contents and file/folder paths** at scaffold time. So every namespace, project name, and directory in `working/content/modulith/` must keep the `ModulithTemplate` prefix — if you introduce an identifier that should be renamed per-project but omit the prefix, it will leak the template's name into generated projects. After any structural change, scaffold into a temp dir (see commands above) and confirm nothing named `ModulithTemplate` survives.
- **`shortName` is `modulith`** — the `dotnet new modulith` invocation name.
- The package project packs `content/**` (excluding `bin`/`obj`) and does not compile anything itself (`IncludeBuildOutput=false`, `Compile Remove="**\*"`).

## Releasing

- Bump `<PackageVersion>` in `working/ModularMonolith.Template.csproj` before releasing.
- CI (`.github/workflows/build.yml`) builds the content solution with `-warnaserror` on every push/PR to `main` — keep it warning-clean.
- Publishing (`.github/workflows/publish.yml`) is triggered by a **published GitHub Release**: it builds Release, `dotnet pack`s, and pushes `Modulith.*.nupkg` to nuget.org using the `NUGET_APIKEY` secret. Cutting a GitHub Release is what ships a version.

## Versioning

Do not perform any git actions unless explicitly asked — the maintainer handles commits, branches, and releases.

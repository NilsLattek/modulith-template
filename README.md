# Modulith

Work in progress...

`dotnet new` templates for building modular monoliths in C#, published to nuget.org as the
[`Modulith`](https://www.nuget.org/packages/Modulith) package.

`dotnet new modulith` scaffolds a .NET solution: a Blazor Server host plus one vertical slice
per feature, each owning its own `Domain`/`Application`/`Infrastructure`/`Web` projects, its own
EF Core `DbContext`, and its own Postgres schema. Features cannot reference each other, and
architecture tests enforce that. `dotnet new modulith-feature` adds another slice to an existing
solution — eight projects, fully wired.

Use it when a set of microservices would be premature but you still want the module boundaries
that make splitting one out later a mechanical job.

Heavily inspired by:
- https://github.com/kgrzybek/modular-monolith-with-ddd

## Install

```bash
dotnet new install Modulith
```

Then scaffold a new solution:

```bash
dotnet new modulith -n MyApp -o MyApp
```

Add another feature slice to an existing generated solution (run from that solution's root):

```bash
dotnet new modulith-feature --appName MyApp -n Payments
```

## Built for AI coding agents

The generated solution ships with a `CLAUDE.md` describing its VSA + DDD architecture, layer
boundaries, and commands. It gives coding agents the structure and guardrails to add or modify
features consistently — where each layer's responsibilities end, how features stay isolated from
one another, and how to verify a change (build, test, architecture tests) before calling it done.

## Development

Test the package:

```bash
dotnet new install working/content/modulith
```

Package:

```bash
cd working
dotnet pack
dotnet new install bin/Release/Modulith.*.nupkg
```

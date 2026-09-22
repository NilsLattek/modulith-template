---
name: adding-a-feature
description: Scaffold a new vertical-slice feature into this solution and register it with the host. Use when adding a new feature, module or slice (a new area of the application owning its own domain, schema and layer projects) — not when adding a command, query or entity to a feature that already exists.
---

# Adding a feature

A feature is one vertical slice owning its own domain model, database schema and layer projects.
Scaffold it — never hand-create the projects, the sub-template wires up the outbox mapping and the
architecture tests expect its exact shape.

## 1. Scaffold

From the **solution root**:

```bash
dotnet new modulith-feature --appName ModulithTemplate -n Shipping
```

`-n` is the feature name in PascalCase, singular or plural as reads best (`Shipping`, `Orders`).
This creates `src/Features/Shipping/` with `Contracts`, `Domain`, `Application`, `Infrastructure`
and `Web` projects, the matching test projects under `test/Features/Shipping/`, and registers all
of them in the `.slnx`.

**`Outbox` is a reserved name** — it would collide with the shared outbox context in
`add-migration.sh` and the CI migration check. Do not create a feature called that.

## 2. Register it with the host

This is the one step the template cannot do for you, and nothing fails until run time if you skip
it — the feature simply never loads.

- Add a project reference from `src/ModulithTemplate.Web` to the feature's `.Web` project.
- Call `builder.ConfigureShippingFeature();` in `src/ModulithTemplate.Web/Program.cs`, alongside
  the existing features.

## 3. First migration

Each feature owns its own `DbContext` and schema, so `--context` is always required; the wrapper
supplies it:

```bash
bash add-migration.sh Shipping InitialCreate
bash update-database.sh
```

## 4. Verify

```bash
dotnet build --no-restore -warnaserror -v minimal
dotnet test --no-restore
```

The architecture tests are the real check: they enforce the layer rules and will fail a feature
whose projects reference each other wrongly.

## What goes in it next

Entities and invariants go in `Domain` — see the entity guidance in `CLAUDE.md`. Commands and
queries follow the `adding-a-command-or-query` skill. If the new feature needs data from an
existing one, or should react to it, follow `reaching-another-feature` — do **not** add a project
reference between features.

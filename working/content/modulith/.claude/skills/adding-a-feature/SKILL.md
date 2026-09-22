---
name: adding-a-feature
description: Decide whether something warrants a new feature, then scaffold it and register it with the host. Use when adding a new feature, module or slice, when choosing between extending an existing feature and creating one, or when sizing feature boundaries — and before running `dotnet new modulith-feature`. Not for adding a command, query or entity to a feature that already exists.
---

# Adding a feature

A feature is one vertical slice owning its own domain model, database schema and layer projects.
Scaffold it — never hand-create the projects, the sub-template wires up the outbox mapping and the
architecture tests expect its exact shape.

**But first decide whether this should be a feature at all.** Most new work should not be.

## 0. Is this actually a feature?

**The default answer is no — put it in an existing feature.** A feature needs positive
justification, not the absence of an objection. "It is new work" is not a reason.

A feature is sized like a **bounded context** in DDD, not like a folder or a screen. It is a
consistency boundary that happens to be deployed with the others. Two features share no types, no
tables and **no transactions** — so the boundary you draw is a boundary you cannot cheaply undo.

### The transaction test

Ask: **must this data change atomically with data an existing feature owns?**

- **Yes** → it belongs *in* that feature. There is no other option. A feature is a transaction
  scope; splitting it means giving up the transaction and accepting eventual consistency.
- **No, it can be eventually consistent** → a separate feature is *permitted*. Now check that it
  earns one.

### What earns a feature

Roughly all of these should hold:

- It owns **at least one aggregate root** with its own lifecycle and invariants — not a single
  entity that is really a lookup table.
- It has **its own language**. The same word means something different here than elsewhere
  (`Order` to Shipping is an address and a weight; to Billing it is a total and a tax code). That
  divergence is the strongest signal of a real boundary.
- It has **several use cases**, not one — a handful of commands and queries, not a single screen.
- It could plausibly be **owned by a different team**, or extracted to its own service later
  without the seams moving.

### What does not earn one

- A single CRUD screen, report, export or admin page. These are `Web` concerns in the feature that
  owns the data.
- One entity with no invariants of its own.
- Anything named after a UI page, a technical layer or a delivery mechanism — `Reports`, `Admin`,
  `Notifications`, `Exports`, `Api`. These cut across features rather than being one.
- Anything that would immediately need **synchronous, read-write** access to another feature's
  data, or a Module API call in both directions. That is one feature that has been split in two.

### Why this matters

A feature is expensive and the cost is permanent: nine projects (five layers, four test), its own
schema and migration history, its own DI wiring and a manual host registration. Worse, every call
across the boundary becomes a Module API or an Integration Event — never a join, never a shared
transaction.

Get the sizing wrong in the direction of *too small* and you get dozens of features that cannot do
anything without talking to each other: chatty Module APIs, eventual consistency where you needed a
transaction, and a distributed monolith with none of the benefits. **Too few features is a
refactor. Too many is a rewrite.** When genuinely unsure, put it in the existing feature — splitting
later is far cheaper than merging.

### Worked examples

| Request | Answer |
| --- | --- |
| "Add order cancellation" | `Orders` — it mutates the Order aggregate inside its invariants |
| "Add an admin page listing every order" | `Orders`, in its `Web` layer — a screen is not a feature |
| "Email the customer when an order ships" | A consumer in the feature that owns the notification, reacting to an Integration Event — not a `Notifications` feature |
| "Track stock levels, with reservation and replenishment rules" | A feature — its own aggregate, its own invariants, its own language, eventually consistent with Orders |
| "Store a customer's preferred currency" | Whichever feature owns the customer — one field is not a boundary |

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

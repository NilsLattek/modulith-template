#!/usr/bin/env bash
# Creates a new EF Core migration in a feature's Infrastructure project, or in the shared
# outbox project.
#
# Usage: bash add-migration.sh <FeatureName|Outbox> <MigrationName>
#   e.g. bash add-migration.sh Orders InitialOrders
#        bash add-migration.sh Outbox AddSomeColumn
set -euo pipefail

cd "$(dirname "$0")"

if [ $# -ne 2 ]; then
  echo "Usage: bash add-migration.sh <FeatureName|Outbox> <MigrationName>" >&2
  echo "  e.g. bash add-migration.sh Orders InitialOrders" >&2
  exit 1
fi

feature=$1
migration=$2

# The outbox is the one context that is not a feature: it lives at SharedKernel level and owns
# the single shared table every feature stages integration events into (ADR 0001). "Outbox" is
# therefore a reserved name here — a feature of that name would collide with this context in
# update-database.sh and the CI migration check too, so do not create one.
if [ "$feature" = "Outbox" ]; then
  project="src/SharedKernel/ModulithTemplate.SharedKernel.Outbox/ModulithTemplate.SharedKernel.Outbox.csproj"
  context=OutboxContext
  test -f "$project" || { echo "The outbox project is missing (expected $project)" >&2; exit 1; }
else
  project="src/Features/$feature/ModulithTemplate.Features.$feature.Infrastructure/ModulithTemplate.Features.$feature.Infrastructure.csproj"
  context="${feature}Context"
  test -f "$project" || { echo "No such feature: $feature (expected $project)" >&2; exit 1; }
fi

host=src/ModulithTemplate.Web

# `dotnet ef` reads project metadata before it builds anything, so it fails outright on a
# never-restored checkout. Building the host up front restores both projects and lets the EF
# command below skip its own build.
dotnet build "$host"

# The migration file has to land in the owning assembly, so --project is that project here —
# unlike update-database.sh, which only reads existing migrations.
dotnet ef migrations add "$migration" -o Data/Migrations --no-build \
  --context "$context" \
  --project "$project" \
  --startup-project "$host"

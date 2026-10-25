#!/usr/bin/env bash
# Creates a new EF Core migration in a feature's Infrastructure project, or for the shared
# outbox or inbox table.
#
# Usage: bash add-migration.sh <FeatureName|Outbox|Inbox> <MigrationName>
#   e.g. bash add-migration.sh Orders InitialOrders
#        bash add-migration.sh Outbox AddSomeColumn
set -euo pipefail

cd "$(dirname "$0")"

if [ $# -ne 2 ]; then
  echo "Usage: bash add-migration.sh <FeatureName|Outbox|Inbox> <MigrationName>" >&2
  echo "  e.g. bash add-migration.sh Orders InitialOrders" >&2
  exit 1
fi

feature=$1
migration=$2

# The outbox and the inbox are the contexts that are not features: they live at SharedKernel level,
# in one project, and each owns one shared table. "Outbox" and "Inbox" are therefore reserved names
# here — a feature of either name would collide with its context in update-database.sh and the CI
# migration check too, so do not create one.
output=Data/Migrations
if [ "$feature" = "Outbox" ] || [ "$feature" = "Inbox" ]; then
  project="src/SharedKernel/ModulithTemplate.SharedKernel.Outbox/ModulithTemplate.SharedKernel.Outbox.csproj"
  context="${feature}Context"
  # The two contexts share a project, so their migrations need folders of their own.
  if [ "$feature" = "Inbox" ]; then output=Data/InboxMigrations; fi
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
dotnet ef migrations add "$migration" -o "$output" --no-build \
  --context "$context" \
  --project "$project" \
  --startup-project "$host"

#!/usr/bin/env bash
# Creates a new EF Core migration in a feature's Infrastructure project.
#
# Usage: bash add-migration.sh <FeatureName> <MigrationName>
#   e.g. bash add-migration.sh Orders InitialOrders
set -euo pipefail

cd "$(dirname "$0")"

if [ $# -ne 2 ]; then
  echo "Usage: bash add-migration.sh <FeatureName> <MigrationName>" >&2
  echo "  e.g. bash add-migration.sh Orders InitialOrders" >&2
  exit 1
fi

feature=$1
migration=$2

project="src/Features/$feature/ModulithTemplate.Features.$feature.Infrastructure/ModulithTemplate.Features.$feature.Infrastructure.csproj"
test -f "$project" || { echo "No such feature: $feature (expected $project)" >&2; exit 1; }

host=src/ModulithTemplate.Web

# `dotnet ef` reads project metadata before it builds anything, so it fails outright on a
# never-restored checkout. Building the host up front restores both projects and lets the EF
# command below skip its own build.
dotnet build "$host"

# The migration file has to land in the feature's own assembly, so --project is the feature's
# Infrastructure project here — unlike update-database.sh, which only reads existing migrations.
dotnet ef migrations add "$migration" -o Data/Migrations --no-build \
  --context "${feature}Context" \
  --project "$project" \
  --startup-project "$host"

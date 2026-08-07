#!/usr/bin/env bash
# Applies every feature's pending migrations to the local database.
#
# Each feature owns its own DbContext, so the contexts are discovered from the host's DI
# container rather than hard-coded here — a new feature is picked up as soon as Program.cs
# calls its ConfigureXxxFeature().
#
# Usage: bash update-database.sh
set -euo pipefail

cd "$(dirname "$0")"

host=src/ModulithTemplate.Web

# Build once up front so the EF commands below can skip their own builds.
dotnet build "$host"

contexts=$(dotnet ef dbcontext list --no-build --json \
  --project "$host" --startup-project "$host" | jq -r '.[].name')
test -n "$contexts" || { echo "No DbContext was discovered in the host" >&2; exit 1; }

for ctx in $contexts; do
  echo
  echo "==> $ctx"
  # EF resolves each context's migrations from the context type, so the host works as
  # --project for every one of them.
  dotnet ef database update --no-build \
    --context "$ctx" --project "$host" --startup-project "$host"
done

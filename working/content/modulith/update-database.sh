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

# --prefix-output tags the JSON lines with "data:", so EF's own info/warn lines (e.g. the
# tools-older-than-runtime warning) can't corrupt it; tr strips Windows' CRLF. sed rather
# than jq, which Git Bash on Windows doesn't ship.
contexts=$(dotnet ef dbcontext list --no-build --json --prefix-output \
  --project "$host" --startup-project "$host" \
  | tr -d '\r' | sed -n 's/^data: *"name": *"\([^"]*\)".*/\1/p')
test -n "$contexts" || { echo "No DbContext was discovered in the host" >&2; exit 1; }

for ctx in $contexts; do
  echo
  echo "==> $ctx"
  # EF resolves each context's migrations from the context type, so the host works as
  # --project for every one of them.
  dotnet ef database update --no-build \
    --context "$ctx" --project "$host" --startup-project "$host"
done

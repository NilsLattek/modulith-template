#!/bin/bash
# Web sessions only: start the Postgres ModulithTemplate.TemplateTests needs and restore packages.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Same server the devcontainer and CI provide: localhost, postgres/postgres.
if command -v pg_isready >/dev/null && ! pg_isready -h localhost -q; then
  service postgresql start
  for _ in $(seq 1 30); do pg_isready -h localhost -q && break; sleep 1; done
fi
if command -v psql >/dev/null; then
  su postgres -c "psql -q -c \"ALTER USER postgres PASSWORD 'postgres';\""
fi

dotnet restore "$CLAUDE_PROJECT_DIR/working/content/modulith/ModulithTemplate.slnx"

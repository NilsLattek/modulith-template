#!/usr/bin/env bash
# Runs once after the devcontainer is created (see devcontainer.json -> postCreateCommand).
# Keep steps idempotent so re-running on an existing container is safe.
set -euo pipefail

# Install external-repo Claude Code plugins that project settings enable but cannot fetch.
# `.claude/settings.json` only *enables* plugins; external-URL plugins (e.g. superpowers,
# which lives in github.com/obra/superpowers) still need a per-machine clone, and a fresh
# container starts with an empty ~/.claude plugin state. `|| true` keeps container creation
# from failing if the marketplace/network isn't ready yet.
claude plugin install superpowers@claude-plugins-official || true

# Add further one-time setup below, e.g.:
# dotnet restore

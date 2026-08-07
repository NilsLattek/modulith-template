#!/usr/bin/env bash
# Runs once after the devcontainer is created (see devcontainer.json -> postCreateCommand).
# Keep steps idempotent so re-running on an existing container is safe.
set -euo pipefail

# EF Core CLI, used for migrations (see CLAUDE.md). `|| true` so a re-run doesn't fail
# when the tool is already installed.
dotnet tool install --global dotnet-ef || true

# Install Claude Code plugins that project settings enable but a fresh container cannot fetch.
# `.claude/settings.json` only *enables* plugins; the actual bits are cloned per machine, and a
# fresh container starts with an empty ~/.claude plugin state.
#
# `claude-plugins-official` is a *default* marketplace name, but its local clone is only fetched
# lazily when an interactive session starts. postCreateCommand runs before any session exists, so
# `plugin install <name>@claude-plugins-official` here fails with "Plugin not found in marketplace"
# unless the marketplace is materialised first. `marketplace add` is idempotent.
# `|| true` keeps container creation from failing if the network isn't ready yet.
claude plugin marketplace add anthropics/claude-plugins-official || true
claude plugin install superpowers@claude-plugins-official || true

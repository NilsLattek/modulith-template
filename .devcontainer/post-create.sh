#!/usr/bin/env bash
# Runs once after the devcontainer is created (see devcontainer.json -> postCreateCommand).
# Keep steps idempotent so re-running on an existing container is safe.
set -euo pipefail

# https://claude.com/plugins/csharp-lsp
dotnet tool install --global csharp-ls || true

# fix login via access token. https://github.com/OLibutzki/claude-marketplace/commit/38c0d4647849255ed3de702d770ac2f629f45385
if [ -n "${CLAUDE_CODE_OAUTH_TOKEN:-}" ] && [ ! -f "$HOME/.claude.json" ]; then
  echo '{"hasCompletedOnboarding": true}' > "$HOME/.claude.json"
fi

# Install the Claude Code CLI via the official installer. Faster than the
# devcontainer feature, which pulls in Node.js first and tends to lag behind
# on the Claude Code version it installs.
curl -fsSL https://claude.ai/install.sh | bash
# The installer places the binary in ~/.local/bin and wires PATH into shell
# rc files, but this script runs non-interactively so those rc files are
# never sourced here. Add it to PATH now so `claude` below resolves.
export PATH="$HOME/.local/bin:$PATH"

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
claude plugin install csharp-lsp@claude-plugins-official || true
claude plugin install superpowers@claude-plugins-official || true

# Add further one-time setup below, e.g.:
# dotnet restore

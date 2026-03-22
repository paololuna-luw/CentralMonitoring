#!/usr/bin/env bash
set -euo pipefail

RUNTIME="${1:-linux-x64}"
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_ROOT="${OUTPUT_ROOT:-dist}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
AGENT_OUTPUT="$REPO_ROOT/$OUTPUT_ROOT/CentralMonitoring.Agent"

echo "Publishing Agent to $AGENT_OUTPUT"
dotnet publish "$REPO_ROOT/CentralMonitoring.Agent/CentralMonitoring.Agent.csproj" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -o "$AGENT_OUTPUT"

echo "Agent publish completed."

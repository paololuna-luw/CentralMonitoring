#!/usr/bin/env bash
set -euo pipefail

OUTPUT_ROOT="${OUTPUT_ROOT:-dist}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
AGENT_SOURCE="$REPO_ROOT/$OUTPUT_ROOT/CentralMonitoring.Agent"

read_required() {
  local prompt="$1"
  local default_value="${2:-}"
  local value=""
  while true; do
    if [[ -n "$default_value" ]]; then
      read -r -p "$prompt [$default_value]: " value
      value="${value:-$default_value}"
    else
      read -r -p "$prompt: " value
    fi
    value="$(printf '%s' "$value" | xargs)"
    [[ -n "$value" ]] && { printf '%s\n' "$value"; return 0; }
    echo "Valor obligatorio."
  done
}

echo
echo "Actualizacion de CentralMonitoring Agent (Linux)"
echo "Preserva appsettings.Production.json existente."
echo

if [[ ! -d "$AGENT_SOURCE" ]]; then
  echo "No existe publish en '$OUTPUT_ROOT'. Ejecuta primero ./scripts/publish-agent-linux.sh" >&2
  exit 1
fi

AGENT_TARGET="$(read_required "Ruta instalada Agent" "/opt/centralmonitoring/agent")"
CFG_BACKUP="$(mktemp)"

[[ -f "$AGENT_TARGET/appsettings.Production.json" ]] && cp "$AGENT_TARGET/appsettings.Production.json" "$CFG_BACKUP"
rm -rf "$AGENT_TARGET"/*
cp -R "$AGENT_SOURCE"/. "$AGENT_TARGET"/
[[ -s "$CFG_BACKUP" ]] && cp "$CFG_BACKUP" "$AGENT_TARGET/appsettings.Production.json"
rm -f "$CFG_BACKUP"

echo
echo "Agent actualizado. Reinicia el servicio."

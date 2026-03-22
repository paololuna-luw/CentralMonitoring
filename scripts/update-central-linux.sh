#!/usr/bin/env bash
set -euo pipefail

OUTPUT_ROOT="${OUTPUT_ROOT:-dist}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
API_SOURCE="$REPO_ROOT/$OUTPUT_ROOT/CentralMonitoring.Api"
WORKER_SOURCE="$REPO_ROOT/$OUTPUT_ROOT/CentralMonitoring.Worker"
UI_DIST_ROOT="$REPO_ROOT/ui/dist"

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

resolve_ui_build_dir() {
  python - "$UI_DIST_ROOT" <<'PY'
import pathlib
import sys
root = pathlib.Path(sys.argv[1])
if not root.exists():
    sys.exit(1)
for path in root.rglob("index.html"):
    print(path.parent)
    sys.exit(0)
sys.exit(1)
PY
}

echo
echo "Actualizacion de CentralMonitoring Central (Linux)"
echo "Preserva appsettings.Production.json existentes."
echo

if [[ ! -d "$API_SOURCE" || ! -d "$WORKER_SOURCE" ]]; then
  echo "No existe publish en '$OUTPUT_ROOT'. Ejecuta primero ./scripts/publish-central-linux.sh" >&2
  exit 1
fi

UI_SOURCE="$(resolve_ui_build_dir)" || {
  echo "No se encontro build de UI en ui/dist. Ejecuta primero ./scripts/publish-central-linux.sh" >&2
  exit 1
}

API_TARGET="$(read_required "Ruta instalada API" "/opt/centralmonitoring/api")"
WORKER_TARGET="$(read_required "Ruta instalada Worker" "/opt/centralmonitoring/worker")"
UI_TARGET="$(read_required "Ruta instalada UI" "/var/www/centralmonitoring-ui")"

API_CFG_BACKUP="$(mktemp)"
WORKER_CFG_BACKUP="$(mktemp)"

[[ -f "$API_TARGET/appsettings.Production.json" ]] && cp "$API_TARGET/appsettings.Production.json" "$API_CFG_BACKUP"
[[ -f "$WORKER_TARGET/appsettings.Production.json" ]] && cp "$WORKER_TARGET/appsettings.Production.json" "$WORKER_CFG_BACKUP"

rm -rf "$API_TARGET"/*
rm -rf "$WORKER_TARGET"/*
rm -rf "$UI_TARGET"/*

cp -R "$API_SOURCE"/. "$API_TARGET"/
cp -R "$WORKER_SOURCE"/. "$WORKER_TARGET"/
cp -R "$UI_SOURCE"/. "$UI_TARGET"/

[[ -s "$API_CFG_BACKUP" ]] && cp "$API_CFG_BACKUP" "$API_TARGET/appsettings.Production.json"
[[ -s "$WORKER_CFG_BACKUP" ]] && cp "$WORKER_CFG_BACKUP" "$WORKER_TARGET/appsettings.Production.json"

rm -f "$API_CFG_BACKUP" "$WORKER_CFG_BACKUP"

echo
echo "Central actualizada."
echo "Reinicia servicios y valida /health."

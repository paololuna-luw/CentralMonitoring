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

read_optional() {
  local prompt="$1"
  local default_value="${2:-}"
  local value=""
  if [[ -n "$default_value" ]]; then
    read -r -p "$prompt [$default_value]: " value
    value="${value:-$default_value}"
  else
    read -r -p "$prompt: " value
  fi
  printf '%s\n' "$(printf '%s' "$value" | xargs)"
}

read_yes_no() {
  local prompt="$1"
  local default_value="${2:-y}"
  local value=""
  while true; do
    if [[ "$default_value" == "y" ]]; then
      read -r -p "$prompt [Y/n]: " value
      value="${value:-y}"
    else
      read -r -p "$prompt [y/N]: " value
      value="${value:-n}"
    fi
    value="$(printf '%s' "$value" | tr '[:upper:]' '[:lower:]' | xargs)"
    case "$value" in
      y|yes) printf 'true\n'; return 0 ;;
      n|no) printf 'false\n'; return 0 ;;
      *) echo "Responde y o n." ;;
    esac
  done
}

generate_secret() {
  python - <<'PY'
import secrets
print(secrets.token_urlsafe(36))
PY
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
echo "Instalacion inicial de CentralMonitoring Central (Linux)"
echo "Orden esperado: publish primero, install despues."
echo

if [[ ! -d "$API_SOURCE" || ! -d "$WORKER_SOURCE" ]]; then
  echo "No existe publish en '$OUTPUT_ROOT'. Ejecuta primero ./scripts/publish-central-linux.sh" >&2
  exit 1
fi

UI_SOURCE="$(resolve_ui_build_dir)" || {
  echo "No se encontro build de UI en ui/dist. Ejecuta primero ./scripts/publish-central-linux.sh" >&2
  exit 1
}

API_TARGET="$(read_required "Ruta final API" "/opt/centralmonitoring/api")"
WORKER_TARGET="$(read_required "Ruta final Worker" "/opt/centralmonitoring/worker")"
UI_TARGET="$(read_required "Ruta final UI" "/var/www/centralmonitoring-ui")"

DB_HOST="$(read_required "PostgreSQL host" "127.0.0.1")"
DB_PORT="$(read_required "PostgreSQL port" "5432")"
DB_NAME="$(read_required "PostgreSQL database" "central_monitoring")"
DB_USER="$(read_required "PostgreSQL user" "central_app")"
DB_PASSWORD="$(read_required "PostgreSQL password")"
API_KEY="$(read_optional "ApiKey local de esta central (dejar vacio para generar una nueva)")"
if [[ -z "$API_KEY" ]]; then
  API_KEY="$(generate_secret)"
  echo "ApiKey generada automaticamente."
fi

CLOUD_ENABLED="$(read_yes_no "Habilitar cloud en el Worker" "y")"
CLOUD_BASE_URL=""
CLOUD_INSTANCE_ID=""
CLOUD_INSTANCE_NAME=""
CLOUD_ORG_NAME=""
CLOUD_ORG_SLUG=""
CLOUD_DESCRIPTION=""
CLOUD_SYNC_API_KEY=""
CLOUD_SYNC_INTERVAL="60"

if [[ "$CLOUD_ENABLED" == "true" ]]; then
  CLOUD_BASE_URL="$(read_required "Cloud BaseUrl" "https://centralmonitoring-cloudapi.example.com")"
  CLOUD_INSTANCE_ID="$(read_required "Cloud InstanceId")"
  CLOUD_INSTANCE_NAME="$(read_required "Cloud InstanceName" "Central Cliente 01")"
  CLOUD_ORG_NAME="$(read_required "Cloud OrganizationName" "Cliente 01")"
  CLOUD_ORG_SLUG="$(read_required "Cloud OrganizationSlug" "cliente-01")"
  CLOUD_DESCRIPTION="$(read_optional "Cloud Description" "$CLOUD_INSTANCE_NAME")"
  CLOUD_SYNC_API_KEY="$(read_optional "Cloud SyncApiKey (opcional)")"
  CLOUD_SYNC_INTERVAL="$(read_required "Cloud SyncIntervalSeconds" "60")"
fi

mkdir -p "$API_TARGET" "$WORKER_TARGET" "$UI_TARGET"
cp -R "$API_SOURCE"/. "$API_TARGET"/
cp -R "$WORKER_SOURCE"/. "$WORKER_TARGET"/
cp -R "$UI_SOURCE"/. "$UI_TARGET"/

CONNECTION_STRING="Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

python - "$REPO_ROOT" "$API_TARGET/appsettings.Production.json" "$WORKER_TARGET/appsettings.Production.json" \
  "$CONNECTION_STRING" "$API_KEY" "$CLOUD_ENABLED" "$CLOUD_BASE_URL" "$CLOUD_INSTANCE_ID" "$CLOUD_INSTANCE_NAME" \
  "$CLOUD_ORG_NAME" "$CLOUD_ORG_SLUG" "$CLOUD_DESCRIPTION" "$CLOUD_SYNC_API_KEY" "$CLOUD_SYNC_INTERVAL" <<'PY'
import json
import pathlib
import sys

repo_root = pathlib.Path(sys.argv[1])
api_dest = pathlib.Path(sys.argv[2])
worker_dest = pathlib.Path(sys.argv[3])
connection_string = sys.argv[4]
api_key = sys.argv[5]
cloud_enabled = sys.argv[6] == "true"
cloud_base_url = sys.argv[7]
cloud_instance_id = sys.argv[8]
cloud_instance_name = sys.argv[9]
cloud_org_name = sys.argv[10]
cloud_org_slug = sys.argv[11]
cloud_description = sys.argv[12]
cloud_sync_api_key = sys.argv[13]
cloud_sync_interval = int(sys.argv[14])

api_cfg = json.loads((repo_root / "CentralMonitoring.Api" / "appsettings.Production.example.json").read_text(encoding="utf-8"))
worker_cfg = json.loads((repo_root / "CentralMonitoring.Worker" / "appsettings.Production.example.json").read_text(encoding="utf-8"))

api_cfg["ConnectionStrings"]["Default"] = connection_string
api_cfg["ApiKey"] = api_key

worker_cfg["ConnectionStrings"]["Default"] = connection_string
worker_cfg["Cloud"]["Enabled"] = cloud_enabled
worker_cfg["Cloud"]["BaseUrl"] = cloud_base_url
worker_cfg["Cloud"]["InstanceId"] = cloud_instance_id
worker_cfg["Cloud"]["InstanceName"] = cloud_instance_name
worker_cfg["Cloud"]["OrganizationName"] = cloud_org_name
worker_cfg["Cloud"]["OrganizationSlug"] = cloud_org_slug
worker_cfg["Cloud"]["Description"] = cloud_description
worker_cfg["Cloud"]["SyncApiKey"] = cloud_sync_api_key
worker_cfg["Cloud"]["SyncIntervalSeconds"] = cloud_sync_interval

api_dest.write_text(json.dumps(api_cfg, indent=2) + "\n", encoding="utf-8")
worker_dest.write_text(json.dumps(worker_cfg, indent=2) + "\n", encoding="utf-8")
PY

echo
echo "Central instalada."
echo "API:    $API_TARGET"
echo "Worker: $WORKER_TARGET"
echo "UI:     $UI_TARGET"
echo "ApiKey: $API_KEY"
echo
echo "Siguiente paso: migraciones, prueba manual y systemd."

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

echo
echo "Instalacion inicial de CentralMonitoring Agent (Linux)"
echo "Orden esperado: publish primero, install despues."
echo

if [[ ! -d "$AGENT_SOURCE" ]]; then
  echo "No existe publish en '$OUTPUT_ROOT'. Ejecuta primero ./scripts/publish-agent-linux.sh" >&2
  exit 1
fi

AGENT_TARGET="$(read_required "Ruta final Agent" "/opt/centralmonitoring/agent")"
API_BASE_URL="$(read_required "ApiBaseUrl de la central" "http://localhost:5188")"
API_KEY="$(read_required "ApiKey emitida por la central")"
HOST_ID="$(read_required "HostId emitido previamente por la central")"
FLUSH_INTERVAL="$(read_required "FlushIntervalSeconds" "15")"
REQUEST_TIMEOUT="$(read_required "RequestTimeoutSeconds" "10")"
BUFFER_FILE_PATH="$(read_required "BufferFilePath" "buffer/agent-buffer.json")"
MAX_BUFFERED_BATCHES="$(read_required "MaxBufferedBatches" "500")"
TOP_PROCESSES_COUNT="$(read_required "TopProcessesCount" "5")"
CRITICAL_UNITS_RAW="$(read_optional "Unidades systemd criticas separadas por coma (opcional)")"

mkdir -p "$AGENT_TARGET"
cp -R "$AGENT_SOURCE"/. "$AGENT_TARGET"/

python - "$REPO_ROOT" "$AGENT_TARGET/appsettings.Production.json" "$API_BASE_URL" "$API_KEY" "$HOST_ID" \
  "$FLUSH_INTERVAL" "$REQUEST_TIMEOUT" "$BUFFER_FILE_PATH" "$MAX_BUFFERED_BATCHES" "$TOP_PROCESSES_COUNT" "$CRITICAL_UNITS_RAW" <<'PY'
import json
import pathlib
import sys

repo_root = pathlib.Path(sys.argv[1])
dest = pathlib.Path(sys.argv[2])
cfg = json.loads((repo_root / "CentralMonitoring.Agent" / "appsettings.Production.example.json").read_text(encoding="utf-8"))

cfg["Agent"]["ApiBaseUrl"] = sys.argv[3]
cfg["Agent"]["ApiKey"] = sys.argv[4]
cfg["Agent"]["HostId"] = sys.argv[5]
cfg["Agent"]["FlushIntervalSeconds"] = int(sys.argv[6])
cfg["Agent"]["RequestTimeoutSeconds"] = int(sys.argv[7])
cfg["Agent"]["BufferFilePath"] = sys.argv[8]
cfg["Agent"]["MaxBufferedBatches"] = int(sys.argv[9])
cfg["Agent"]["TopProcessesCount"] = int(sys.argv[10])
cfg["Agent"]["CriticalLinuxSystemdUnits"] = [item.strip() for item in sys.argv[11].split(",") if item.strip()]
cfg["Agent"]["CriticalWindowsServices"] = []

dest.write_text(json.dumps(cfg, indent=2) + "\n", encoding="utf-8")
PY

echo
echo "Agent instalado en $AGENT_TARGET"
echo "Este agente no se auto-registra. Usa HostId existente."

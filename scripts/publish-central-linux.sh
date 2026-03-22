#!/usr/bin/env bash
set -euo pipefail

RUNTIME="${1:-linux-x64}"
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_ROOT="${OUTPUT_ROOT:-dist}"
UI_API_BASE="${UI_API_BASE:-}"
UI_API_KEY="${UI_API_KEY:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

API_OUTPUT="$REPO_ROOT/$OUTPUT_ROOT/CentralMonitoring.Api"
WORKER_OUTPUT="$REPO_ROOT/$OUTPUT_ROOT/CentralMonitoring.Worker"
UI_ENV_PATH="$REPO_ROOT/ui/src/app/env.ts"
UI_ENV_BACKUP="$(mktemp)"

cp "$UI_ENV_PATH" "$UI_ENV_BACKUP"
restore_ui_env() {
  cp "$UI_ENV_BACKUP" "$UI_ENV_PATH"
  rm -f "$UI_ENV_BACKUP"
}
trap restore_ui_env EXIT

if [[ -n "$UI_API_BASE" && -n "$UI_API_KEY" ]]; then
  cat > "$UI_ENV_PATH" <<EOF
export const env = {
  apiBase: '$UI_API_BASE',
  apiKey: '$UI_API_KEY'
};
EOF
fi

echo "Publishing API to $API_OUTPUT"
dotnet publish "$REPO_ROOT/CentralMonitoring.Api/CentralMonitoring.Api.csproj" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -o "$API_OUTPUT"

echo "Publishing Worker to $WORKER_OUTPUT"
dotnet publish "$REPO_ROOT/CentralMonitoring.Worker/CentralMonitoring.Worker.csproj" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -o "$WORKER_OUTPUT"

echo "Building UI"
cd "$REPO_ROOT/ui"
if [[ ! -d node_modules ]]; then
  npm install
fi
npm run build

echo "Central publish completed."

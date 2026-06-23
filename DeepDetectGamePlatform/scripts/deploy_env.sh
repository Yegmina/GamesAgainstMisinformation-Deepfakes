#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <env_name> <compose_override> [ttl_seconds]"
  exit 1
fi

ENV_NAME="$1"
COMPOSE_OVERRIDE="$2"
TTL_SECONDS="${3:-0}"
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_NAME="deepdetect_${ENV_NAME}"

if [[ "$COMPOSE_OVERRIDE" != /* ]]; then
  COMPOSE_OVERRIDE="$ROOT_DIR/$COMPOSE_OVERRIDE"
fi

if [ ! -f "$COMPOSE_OVERRIDE" ]; then
  echo "Compose override not found: $COMPOSE_OVERRIDE"
  exit 1
fi

RUNTIME_BASE_DIR="${RUNTIME_BASE_DIR:-$ROOT_DIR/runtime}"
export RUNTIME_DIR="$RUNTIME_BASE_DIR/$ENV_NAME"
export APP_ENV="$ENV_NAME"
export COMMIT_SHA="${COMMIT_SHA:-$(git -C "$ROOT_DIR/.." rev-parse --short HEAD 2>/dev/null || echo unknown)}"
export AI_PROVIDER="${AI_PROVIDER:-openai}"

mkdir -p "$RUNTIME_DIR/data"
chmod 0777 "$RUNTIME_DIR/data"

docker compose \
  -p "$PROJECT_NAME" \
  -f "$ROOT_DIR/docker-compose.yml" \
  -f "$COMPOSE_OVERRIDE" \
  down --remove-orphans || true

docker compose \
  -p "$PROJECT_NAME" \
  -f "$ROOT_DIR/docker-compose.yml" \
  -f "$COMPOSE_OVERRIDE" \
  up -d --build

if [ "$TTL_SECONDS" -gt 0 ]; then
  TOKEN="$(date +%s)"
  echo "$TOKEN" > "$RUNTIME_DIR/ttl_token"
  nohup bash -c "sleep '$TTL_SECONDS'; if [ \"\$(cat '$RUNTIME_DIR/ttl_token' 2>/dev/null)\" = '$TOKEN' ]; then docker compose -p '$PROJECT_NAME' -f '$ROOT_DIR/docker-compose.yml' -f '$COMPOSE_OVERRIDE' down --remove-orphans; fi" \
    > "$RUNTIME_DIR/teardown.log" 2>&1 &
fi

#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <base_url> [timeout_seconds]"
  exit 1
fi

BASE_URL="${1%/}"
TIMEOUT_SECONDS="${2:-180}"
INTERVAL_SECONDS=5
START_TIME="$(date +%s)"

while true; do
  if curl -fsS "$BASE_URL/health" >/dev/null; then
    echo "Healthy: $BASE_URL/health"
    exit 0
  fi

  NOW="$(date +%s)"
  if [ $((NOW - START_TIME)) -ge "$TIMEOUT_SECONDS" ]; then
    echo "Health check timed out after ${TIMEOUT_SECONDS}s: $BASE_URL/health"
    curl -sS "$BASE_URL/health" || true
    echo
    exit 1
  fi

  sleep "$INTERVAL_SECONDS"
done

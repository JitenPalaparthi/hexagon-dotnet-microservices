#!/bin/sh
set -eu

CONNECT_URL="${CONNECT_URL:-http://connect:8083}"

echo "Waiting for Kafka Connect at $CONNECT_URL ..."
until curl -sf "$CONNECT_URL/connectors" >/dev/null 2>&1; do
  echo "  ... still waiting"; sleep 3
done

# Collect JSON files safely; tolerate zero files
FILES=$(ls /connectors/*.json 2>/dev/null || true)
if [ -z "$FILES" ]; then
  echo "❌ No JSON files found in /connectors"
  ls -la /connectors || true
  exit 1
fi

post() {
  f="$1"
  # normalize CRLF -> LF in case the file came from Windows
  tmp="/tmp/$(basename "$f")"
  tr -d '\r' < "$f" > "$tmp"

  echo "Registering: $(basename "$f")"
  code="$(curl -s -o /tmp/resp -w '%{http_code}' \
    -H 'Content-Type: application/json' -H 'Expect:' \
    --data-binary @"$tmp" \
    -X POST "$CONNECT_URL/connectors")"

  if [ "$code" = "201" ]; then
    echo "  ✅ Created"
  elif [ "$code" = "409" ]; then
    echo "  ⚠️  Already exists (409), skipping"
  else
    echo "  ❌ HTTP $code"; cat /tmp/resp; exit 1
  fi
}

for f in $FILES; do
  post "$f"
done

echo "All connectors posted."
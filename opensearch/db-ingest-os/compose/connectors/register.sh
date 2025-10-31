#!/bin/sh
set -euxo pipefail

: "${CONNECT_URL:?CONNECT_URL is not set}"

echo "CONNECT_URL=$CONNECT_URL"
echo "Contents of /work:"
ls -lah /work || true

echo "Installing tools..."
apk add --no-cache curl jq >/dev/null

echo "Waiting for Kafka Connect at $CONNECT_URL ..."
for i in $(seq 1 90); do
  if curl -fsS "$CONNECT_URL/connector-plugins" >/dev/null; then
    echo "Connect is up"; break
  fi
  echo "Connect not ready yet (attempt $i) ..."
  sleep 2
done

echo "Registering/updating connectors from /work ..."
found=false
for f in /work/*.json; do
  [ -e "$f" ] || continue
  found=true
  echo "Processing file: $f"
  name=$(jq -r '.name' "$f")
  if [ "$name" = "null" ] || [ -z "$name" ]; then
    echo "ERROR: file $f has no .name field"; continue
  fi
  echo "→ upsert $name"
  if curl -fsS "$CONNECT_URL/connectors/$name" >/dev/null 2>&1; then
    curl -sS -X PUT "$CONNECT_URL/connectors/$name/config" \
         -H 'Content-Type: application/json' -H 'Expect:' \
         --data @"$f"
  else
    curl -sS -X POST "$CONNECT_URL/connectors" \
         -H 'Content-Type: application/json' -H 'Expect:' \
         --data @"$f"
  fi
  echo
done
[ "$found" = true ] || echo "WARNING: no *.json files found in /work"
echo "All done."

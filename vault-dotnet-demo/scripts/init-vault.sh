#!/usr/bin/env bash
set -euo pipefail

VAULT_ADDR="${VAULT_ADDR:-http://127.0.0.1:8200}"
VAULT_TOKEN="${VAULT_TOKEN:-root}"

echo "Using VAULT_ADDR=$VAULT_ADDR"
echo "Using VAULT_TOKEN=$VAULT_TOKEN"

# Use Dockerized vault CLI if host doesn't have it
if ! command -v vault >/dev/null 2>&1; then
  echo "vault CLI not found, running via docker exec (container: vault-dev)"
  VAULT_CLI="docker exec -e VAULT_ADDR=$VAULT_ADDR -e VAULT_TOKEN=$VAULT_TOKEN vault-dev vault"
else
  VAULT_CLI="vault"
fi

# Enable kv v2 at 'secret' (idempotent)
set +e
$VAULT_CLI secrets enable -version=2 -path=secret kv
set -e

# Put demo data (flat keys that map neatly into .NET configuration)
$VAULT_CLI kv put secret/app \
  "MyApp:ApiKey=dev-123-override-from-vault" \
  "MyApp:DbPassword=p@ssw0rd-from-vault"
echo "Wrote secret at secret/app"

echo "Reading back:"
$VAULT_CLI kv get secret/app

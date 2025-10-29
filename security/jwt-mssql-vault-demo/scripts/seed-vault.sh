#!/usr/bin/env sh
set -eu

VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"
VAULT_TOKEN="${VAULT_TOKEN:-root}"

echo "Waiting for Vault API at $VAULT_ADDR ..."
until curl -sf -H "X-Vault-Token: $VAULT_TOKEN" "$VAULT_ADDR/v1/sys/health" >/dev/null; do
  sleep 1
done
echo "Vault is up."

# Tune KV to v2 (idempotent; ignore error if already v2)
echo "Tuning 'secret/' to KV v2..."
curl -s -o /dev/null -w "%{http_code}\n" -X POST "$VAULT_ADDR/v1/sys/mounts/secret/tune" \
  -H "X-Vault-Token: $VAULT_TOKEN" -H "Content-Type: application/json" \
  -d '{"options":{"version":"2"}}' || true

# Seed secret (NO trailing slash in path)
echo "Seeding secret at secret/data/jwt ..."
curl -sf -X POST "$VAULT_ADDR/v1/secret/data/jwt" \
  -H "X-Vault-Token: $VAULT_TOKEN" -H "Content-Type: application/json" \
  -d '{"data":{"secret":"super_dev_jwt_secret_please_change"}}'

# Verify
echo "Verify:"
curl -sf -H "X-Vault-Token: $VAULT_TOKEN" "$VAULT_ADDR/v1/secret/data/jwt"
echo
echo "Done."
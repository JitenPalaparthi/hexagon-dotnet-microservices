# .NET 9 JWT Auth + MS SQL + Vault (Dev) — Downloadable Demo

This demo shows a **.NET 9 Web API** with **JWT authentication**, users stored in **MS SQL Server**, and the **JWT signing secret pulled from HashiCorp Vault** at startup.

## What you get
- **JwtApi/** — .NET 9 minimal API
- **MSSQL 2022** container (sa: `YourStrong!Passw0rd`)
- **Vault (dev mode)** container with root token `root` and a pre-seeded secret at `secret/data/jwt` → `{ secret: "super_dev_jwt_secret_please_change" }`
- `docker-compose.yml` to run the entire stack

## Run
```bash
docker compose up --build
```

Wait until you see `Now listening on: http://0.0.0.0:8088` from `api`.

## Endpoints
- `POST /register`  — body: `{ "username": "jiten", "password": "pass" }`
- `POST /login`     — body: `{ "username": "jiten", "password": "pass" }` → returns `{ token: "..." }`
- `GET  /me`        — **requires** `Authorization: Bearer <token>`
- `GET  /health`    — basic health

## Quick test
```bash
# register
curl -s http://localhost:8088/register -H "Content-Type: application/json" -d '{ "username":"jiten", "password":"pass" }' | jq

# login
TOKEN=$(curl -s http://localhost:8088/login -H "Content-Type: application/json" -d '{ "username":"jiten", "password":"pass" }' | jq -r .token)

# call protected
curl -s http://localhost:8088/me -H "Authorization: Bearer $TOKEN" | jq
```

## Vault
- Dev server at http://localhost:8200 (root token: `root`)
- Secret path: `secret/data/jwt` with data `{ "secret": "<value>" }`

## Change the secret
```bash
curl -s -X POST http://localhost:8200/v1/secret/data/jwt   -H "X-Vault-Token: root" -H "Content-Type: application/json"   -d '{ "data": { "secret": "new_secret_value_here" } }'
```

Restart `api` (or add reload logic) to pick up changes.

## Notes
- For demo simplicity, the DB is auto-created with `EnsureCreated`. In production, use EF migrations.
- Passwords are hashed with **BCrypt**.
- JWTs are signed using **HS256** (symmetric key) from Vault.
- Do **not** use Vault dev server or the `root` token in production. Use proper auth methods (Kubernetes, AppRole, Azure, etc.).


### If issues with init .. Seed now..and then try again

# 1) Verify KV v2 mount exists at "secret/"
curl -s -H "X-Vault-Token: root" http://localhost:8200/v1/sys/mounts | jq '."secret/"'

# 2) Seed the secret at secret/data/jwt (KV v2 requires /data/)
curl -s -X POST http://localhost:8200/v1/secret/data/jwt \
  -H "X-Vault-Token: root" -H "Content-Type: application/json" \
  -d '{ "data": { "secret": "super_dev_jwt_secret_please_change" } }' | jq

# 3) Verify it’s there
curl -s -H "X-Vault-Token: root" http://localhost:8200/v1/secret/data/jwt | jq
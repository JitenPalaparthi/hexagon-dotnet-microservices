# .NET 8 + HashiCorp Vault (KV v2) configuration demo

This shows a minimal, production-style pattern to **fetch secrets from Vault** at startup and **merge** them into the standard .NET `IConfiguration`, so you can consume them via `IOptions<T>` without changing the rest of your app.

## What's inside
- `docker-compose.yml` – spins up Vault in **dev mode** on `http://127.0.0.1:8200` with root token `root` (for local only).
- `scripts/init-vault.sh` – enables KV v2 and writes a demo secret at `secret/app`.
- `src/Program.cs` – builds a generic host, **pulls keys from Vault** using **VaultSharp**, and merges them **as highest-precedence config**.
- `src/appsettings.json` – optional fallbacks, overridden by Vault if present.

## Run it

```bash
# 1) Start Vault dev
docker compose up -d

# 2) Seed demo secrets
./scripts/init-vault.sh

# 3) Run the .NET app (env vars can be changed as needed)
export VAULT_ADDR=http://127.0.0.1:8200
export VAULT_TOKEN=root
export VAULT_MOUNT=secret
export VAULT_PATHS=app           # comma-separated if multiple, e.g. "app,shared,prod/app"

dotnet run --project src
```

Expected output shows `MyApp:ApiKey` and `MyApp:DbPassword` resolved **from Vault**.

## How it works

- We authenticate with a **Token** (dev only). For production, switch to:
  - Kubernetes auth (JWT), AppRole, OIDC/JWT, AWS/GCP/ Azure auth, etc.
- We read KV **v2**: `mount = secret`, `path = app` → full API path is `secret/data/app`.
- Returned JSON is **flattened** into `key=value` pairs (e.g., `MyApp:ApiKey=...`) and added via `AddInMemoryCollection`, which sits **above** other providers (env, appsettings, etc.).

## Using in a real app

Replace the demo printing with your services. You can inject `IOptions<MyAppOptions>` anywhere:

```csharp
public class NeedsSecrets(MyAppOptions opts)
{
    public void Run() => Console.WriteLine(opts.ApiKey);
}
```

## Security notes (important)

- **Never** use the dev root token outside localhost. Use a dedicated auth method and least-privilege policy:
  ```hcl
  # example policy (read-only for the app path)
  path "secret/data/app" {
    capabilities = ["read"]
  }
  ```
- Prefer **short-lived tokens** and **renewal**.
- Avoid writing secrets to logs. This sample prints for demo only.

---

Made for quick copy/paste into your own service. Keep the Vault client creation in a small composition root and wire everything else with `IOptions<T>`.

# Microservice Architecture Demo (.NET 9) — Principles + Downloadable Example

This repo demonstrates core **microservice principles** with runnable code:

## Principles → Where to see them

1. **Service Boundaries & Autonomy**
   - Separate services: `CatalogService`, `OrderService`, `ApiGateway`.
   - Each has its own `appsettings.json` and lifecycle.

2. **API First & Versioning**
   - URL segment versioning `/v1/...` using `Microsoft.AspNetCore.Mvc.Versioning` in both services.

3. **Configuration & Feature Flags**
   - Global config in `_global/appsettings.shared.json`, loaded by all services via `ServiceDefaults`.
   - Optional Azure App Configuration wiring (set `Global__AppConfig__Endpoint`).

4. **Resilience (Retries/Timeouts/Circuit Breaker)**
   - `OrderService` uses `AddStandardResilienceHandler()` on the `HttpClient("catalog")` to call Catalog robustly.

5. **Idempotency**
   - `OrderService` caches responses by `Idempotency-Key` header for POST `/v1/orders`.

6. **Data Consistency (Outbox pattern)**
   - `OrderService` has an in-memory **Outbox** (`/v1/orders/outbox`) to show event staging (simulation).

7. **Observability (Tracing & Metrics)**
   - OpenTelemetry wired in `ServiceDefaults` with **console exporter** (no infra required).

8. **Correlation ID**
   - `X-Correlation-Id` middleware adds/propagates correlation across logs and responses.

9. **Health Checks**
   - `/health` endpoint in each service.

10. **Rate Limiting**
    - Fixed-window limiter in `ServiceDefaults` via `Microsoft.AspNetCore.RateLimiting`.

11. **API Gateway**
    - `ApiGateway` with **YARP** routes: `/catalog/*` → Catalog, `/orders/*` → Orders.

12. **Docs/Discoverability**
    - Swagger UI enabled in Development for all services.

13. **Containers & Orchestration**
    - `Dockerfile` per service and `docker-compose.yml` to run all together.

## Run locally (without Docker)

```bash
dotnet build

# Terminal 1
dotnet run --project CatalogService

# Terminal 2
dotnet run --project OrderService

# Terminal 3
dotnet run --project ApiGateway
```

### Try it
- Catalog: `GET http://localhost:5001/v1/products`
- Orders (create): `POST http://localhost:5003/v1/orders` (optional header `Idempotency-Key: any-string`)
- Orders outbox: `GET http://localhost:5003/v1/orders/outbox`
- Via gateway:
  - `GET http://localhost:5080/catalog/v1/products`
  - `POST http://localhost:5080/orders/v1/orders`
- Health: `GET /health` on each service.
- Swagger UIs:
  - `http://localhost:5001/swagger`
  - `http://localhost:5003/swagger`
  - `http://localhost:5080/swagger`

## Run with Docker
```bash
docker-compose build
docker-compose up
# Gateway: http://localhost:5080
```

## Global config live reload
Edit `_global/appsettings.shared.json` and refresh endpoints (e.g., `/v1/products`) to see updated values (reloaded on change).

## Optional: Azure App Configuration
1. Set an environment variable:
   - PowerShell: `$env:Global__AppConfig__Endpoint = "https://<your-appconfig>.azconfig.io"`
   - bash: `export Global__AppConfig__Endpoint="https://<your-appconfig>.azconfig.io"`
2. Use labels `global` and `Development`. Create a `sentinel` key to trigger app-wide refresh.

## Code reading guide
- **ServiceDefaults/ServiceSetup.cs**: config layering, OpenTelemetry, correlation, rate limiting.
- **CatalogService/Program.cs**: versioned minimal API + in-memory repository.
- **OrderService/Program.cs**: resilient HttpClient, idempotency, outbox.
- **ApiGateway/Program.cs**: YARP reverse proxy with in-memory routes.

> This is a learning/demo setup. For production, add persistent storage, proper message brokers, secret stores, and CI/CD.

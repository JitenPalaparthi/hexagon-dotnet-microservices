# .NET 9 + PostgreSQL CRUD API — Kubernetes Ready

This repo contains a minimal ASP.NET Core 9 Web API that performs CRUD operations on `Item` entities using Entity Framework Core and PostgreSQL. It includes Docker and Kubernetes manifests to run locally with `kubectl` (KinD, Minikube, Docker Desktop, etc.).

## Features

- Minimal API endpoints: `GET/POST/PUT/DELETE /items`
- PostgreSQL (official image) with PVC
- EF Core (code-first) using `EnsureCreated()` for demo simplicity
- Health check at `/health`
- Swagger UI at `/swagger`
- K8s manifests for Postgres + API (`Deployment` + `Service`) and optional `Ingress`

## Prereqs

- Docker
- .NET 9 SDK
- kubectl + a running Kubernetes (Docker Desktop, Minikube, or KinD)
- (Optional) NGINX Ingress Controller if using `ingress.yaml`

## Local run (without Kubernetes)

```bash
# 1) Start Postgres locally (Docker)
docker run -d --name pg -p 5432:5432 \
  -e POSTGRES_USER=app \
  -e POSTGRES_PASSWORD=changeme \
  -e POSTGRES_DB=cruddb \
  -v pgdata:/var/lib/postgresql/data \
  postgres:16

# 2) Run API
cd src/CrudApi
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=cruddb;Username=app;Password=changeme"
dotnet run

# Swagger: http://localhost:5080/swagger (port printed by dotnet; set ASPNETCORE_URLS if needed)
```

## Containerize

```bash
# Build the image
docker build -t crudapi:local .

# Run
docker run -d --name crudapi -p 8080:8080 \
  -e DB_CONNECTION="Host=host.docker.internal;Port=5432;Database=cruddb;Username=app;Password=changeme" \
  crudapi:local
```

## Kubernetes deploy

> All manifests use namespace `crud-demo`.

```bash
# Create namespace + Postgres + API + Service
kubectl apply -f k8s/postgres.yaml
kubectl apply -f k8s/api.yaml

# (Optional) Ingress (requires NGINX Ingress Controller)
kubectl apply -f k8s/ingress.yaml
```

### Access the API

**Option A: port-forward**

```bash
kubectl -n crud-demo port-forward svc/crudapi 8080:8080
# Now browse http://localhost:8080/swagger
```

**Option B: Ingress**

- Add `crud.local` to `/etc/hosts` pointing to your Ingress controller IP (often `127.0.0.1` on Docker Desktop/Minikube with tunnel):
  ```
  127.0.0.1 crud.local
  ```
- Browse http://crud.local/

## Environment & Secrets

- The API reads the connection string from `DB_CONNECTION` env var (via Secret `api-secret`).
- Postgres password is stored in `postgres-secret` Secret.
- Change default passwords before production use.

## EF Migrations (optional)

This demo uses `EnsureCreated()` for simplicity. For production, switch to `db.Database.Migrate()` and create migrations:

```bash
cd src/CrudApi
dotnet tool install --global dotnet-ef
dotnet add package Microsoft.EntityFrameworkCore.Design --prerelease
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## API Contract

- `GET    /items` → 200 OK `[{ id, name, price, inStock }]`
- `GET    /items/{id}` → 200 OK or 404
- `POST   /items` (JSON: `{ name, price, inStock }`) → 201 Created with body
- `PUT    /items/{id}` (JSON: `{ name, price, inStock }`) → 204 No Content or 404
- `DELETE /items/{id}` → 204 No Content or 404

## Health

- `GET /health` → 200 once DB is reachable.

## Notes

- For a production-grade Postgres, use a `StatefulSet` with proper storage class, backups, and monitoring.
- Add resource limits/requests, probes, and liveness/readiness as needed.

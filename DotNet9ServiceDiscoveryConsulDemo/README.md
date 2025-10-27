# Service Discovery in .NET 9 (Consul + YARP) — Fixed Version

- Added missing reference: `Microsoft.Extensions.Hosting.Abstractions (9.0.0)` to `ServiceDefaults.csproj`.
- Everything else same as before.

## Run (Docker)
docker-compose up --build

## Run (local)
consul agent -dev -ui
dotnet run --project CatalogService
dotnet run --project OrderService
dotnet run --project ApiGateway

# .NET 9 gRPC (Protocol Buffers) — Customer CRUD with PostgreSQL (Server Only)

This server exposes **gRPC** endpoints (Protocol Buffers) to perform CRUD on `Customer` stored in **PostgreSQL**.
Use **Postman (gRPC)** or `grpcurl` to invoke.

## Prerequisites
- .NET 9 SDK
- PostgreSQL running locally (or set `POSTGRES_CONNECTION` env var)

Default connection string (in `appsettings.json`):
```
Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=customerdb
```

## Run
```bash
cd CustomerGrpcServer
dotnet run
```
- Listens on **http://localhost:5000** (HTTP/2 h2c)
- Creates database tables on first run
- Reflection enabled in Development

## Use with Postman (gRPC)
1. New → gRPC
2. Server: `localhost:5000`
3. Import proto: `Protos/customer.proto`
4. Pick service `customer.CustomerManager` and a method
5. Example bodies:

**CreateCustomer**
```json
{ "name":"Ada Lovelace", "email":"ada@example.com" }
```

**GetCustomer**
```json
{ "id":"GUID_FROM_CREATE" }
```

**UpdateCustomer**
```json
{ "id":"GUID_FROM_CREATE", "name":"Ada L.", "email":"ada.l@example.com" }
```

**DeleteCustomer**
```json
{ "id":"GUID_FROM_CREATE" }
```

**ListCustomers**
```json
{ "pageSize": 50, "pageToken": "0" }
```

## grpcurl examples
```bash
grpcurl -plaintext -import-path Protos -proto customer.proto -d '{"name":"Ada","email":"ada@example.com"}' localhost:5000 customer.CustomerManager/CreateCustomer

grpcurl -plaintext -import-path Protos -proto customer.proto -d '{}' localhost:5000 customer.CustomerManager/ListCustomers
```

## Notes
- Uses **h2c** for dev simplicity. For prod, enable HTTPS/TLS.
- Uses `EnsureCreated()`; prefer EF Migrations for production.

### db details 

```bash

docker network create demo-netowrk

docker network ls 

docker run -d  --name pg-customerdb  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres  -e POSTGRES_DB=customerdb -p 5432:5432 --network demo-network -v pgdata_customerdb:/var/lib/postgresql/data  postgres:16

docker run -d --name dbui -p 8080:8080 --network demo-network adminer
```

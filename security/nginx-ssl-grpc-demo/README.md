# NGINX + SSL + .NET 9 (REST + gRPC)

This stack terminates **TLS at NGINX** and routes to:
- `https://localhost/svc1/` → Service A (REST, HTTP/1.1 proxy)
- `https://localhost/svc2/` → Service B (REST, HTTP/1.1 proxy)
- `https://localhost/grpc`  → gRPC service (HTTP/2, `grpc_pass` to h2c backend)

## Prereqs
- Docker + Docker Compose
- OpenSSL (for local cert generation)

## 1) Generate local certs for NGINX
```bash
cd certs
./generate-certs.sh
# outputs: ca.crt, server.crt, server.key
# (Optionally) trust ca.crt in your OS
```

## 2) Build and run
```bash
docker compose up --build
```

## 3) Test REST
```bash
curl -k https://localhost/healthz
curl -k https://localhost/svc1/
curl -k https://localhost/svc2/
```

## 4) Test gRPC
Using grpcurl (install from https://github.com/fullstorydev/grpcurl)

### With server reflection (enabled in Development)
```bash
grpcurl -insecure https://localhost:443 list
grpcurl -insecure https://localhost:443 demo.Greeter/SayHello -d '{ "name": "Jiten" }'
```

### With proto (no reflection)
```bash
grpcurl -insecure -proto GrpcService/Protos/greeter.proto   https://localhost:443 demo.Greeter/SayHello -d '{ "name": "Jiten" }'
```

> `-insecure` is fine for local self-signed certs. Remove it once you trust `certs/ca.crt` locally.

## 5) How it works
- NGINX listens on **443 with http2** and uses `server.crt` + `server.key`.
- `/grpc` uses `grpc_pass grpc://grpc_upstream;` to talk h2c to the .NET gRPC service on port 8090.
- REST routes use `proxy_pass` to HTTP/1.1 minimal APIs on port 8080.
- The gRPC backend runs **HTTP/2 without TLS** (h2c); TLS is terminated at NGINX.

## 6) Production notes
- Replace self-signed certs with a **real CA** (Let’s Encrypt / DigiCert).
- For SNI/host-based routing, use multiple `server {}` blocks and `server_name`.
- For **mTLS**, configure in NGINX: `ssl_verify_client on;` and set `ssl_client_certificate` to your client-CA.

Enjoy!

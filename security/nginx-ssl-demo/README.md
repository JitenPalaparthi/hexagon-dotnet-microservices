# NGINX + SSL with Two .NET 9 Services (Path Routing)

This demo shows how to terminate **TLS/SSL at NGINX** and reverse-proxy to two **.NET 9 minimal APIs** behind it:
- `https://localhost/svc1/` → Service A
- `https://localhost/svc2/` → Service B

## Prereqs
- Docker + Docker Compose
- OpenSSL (for local self-signed cert generation)

## 1) Generate local SSL certs for NGINX
```bash
cd certs
./generate-certs.sh
# This creates: ca.crt, server.crt, server.key (and helpers)
# (Optional) Import ca.crt into your OS trust store to avoid browser warnings.
```

## 2) Build & run
```bash
docker compose up --build
```
NGINX listens on:
- **80** → redirects to 443
- **443** → terminates TLS on `server.crt`/`server.key`

## 3) Test
- `curl -k https://localhost/healthz` → `ok`
- `curl -k https://localhost/svc1/` → JSON from Service A
- `curl -k https://localhost/svc2/` → JSON from Service B

> Remove `-k` once you import `certs/ca.crt` into your OS trust store.

## 4) How it works
- NGINX serves TLS using `certs/server.crt` + `certs/server.key`.
- Path rules:
  - `/svc1/*` → upstream `service_a:8080`
  - `/svc2/*` → upstream `service_b:8080`
- .NET services listen on `8080` **inside the containers** (`ASPNETCORE_URLS=http://0.0.0.0:8080`).

## 5) Switching to a real (public) certificate
In production:
- Use **Let’s Encrypt** or a commercial CA.
- Put the issued `fullchain.pem` and `privkey.pem` (or `.crt`/`.key`) in your NGINX volume.
- Update the NGINX `ssl_certificate` and `ssl_certificate_key` paths accordingly.
- Automate renewal (e.g., certbot or Kubernetes cert-manager).

## 6) Notes
- This config is **HTTP/1.1** reverse proxy for REST APIs. For **gRPC**, use `grpc_pass` and ensure `http2` upstreams.
- If you need host-based routing (`api.local` → ServiceA), adjust `server_name` and `location` or use separate `server {}` blocks.
- For mTLS (mutual TLS), add `ssl_verify_client on;` and `ssl_client_certificate` to NGINX, and configure services accordingly.

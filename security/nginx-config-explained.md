# NGINX Reverse Proxy (REST + gRPC) — Full Config Walkthrough & Production Notes

This document explains each stanza of the provided NGINX config (TLS, REST proxy, gRPC proxy), and adds **production-grade** guidance for caching, timeouts, compression, connection reuse, logging, and rate limiting.

---

## Full Config (for reference)

```nginx
user  nginx;
worker_processes  auto;

events { worker_connections 1024; }

http {
  # Basic tuning
  sendfile on;
  tcp_nopush on;
  tcp_nodelay on;
  keepalive_timeout 65;
  types_hash_max_size 4096;
  include       /etc/nginx/mime.types;
  default_type  application/octet-stream;

  # Upstreams
  upstream service_a_upstream { server service_a:8080; }
  upstream service_b_upstream { server service_b:8080; }
  upstream grpc_upstream      { server grpc_service:8090; } # h2c backend

  server {
    listen 80;
    server_name localhost;
    return 301 https://$host$request_uri;
  }

  server {
    listen 443 ssl http2;
    server_name localhost;

    ssl_certificate     /etc/nginx/certs/server.crt;
    ssl_certificate_key /etc/nginx/certs/server.key;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;

    # REST services (HTTP/1.1 proxy)
    location /svc1/ {
      proxy_set_header Host $host;
      proxy_set_header X-Forwarded-For $remote_addr;
      proxy_set_header X-Forwarded-Proto $scheme;
      proxy_set_header X-Forwarded-Prefix /svc1;
      rewrite ^/svc1/?(.*)$ /$1 break;
      proxy_pass http://service_a_upstream;
    }

    location /svc2/ {
      proxy_set_header Host $host;
      proxy_set_header X-Forwarded-For $remote_addr;
      proxy_set_header X-Forwarded-Proto $scheme;
      proxy_set_header X-Forwarded-Prefix /svc2;
      rewrite ^/svc2/?(.*)$ /$1 break;
      proxy_pass http://service_b_upstream;
    }

    # gRPC route (full-duplex over HTTP/2)
    location /grpc {
      grpc_set_header Host $host;
      grpc_set_header X-Forwarded-Proto $scheme;
      grpc_set_header X-Forwarded-For $remote_addr;
      grpc_pass grpc://grpc_upstream;
    }

    # Health
    location = /healthz { return 200 "ok"; add_header Content-Type text/plain; }
  }
}
```

---

## Top-level (global) directives

- `user nginx;` — NGINX worker processes run as the `nginx` user. Ensure certs/logs are readable by this user.
- `worker_processes auto;` — One worker per CPU core; good default for parallelism.

### `events` block
```nginx
events { worker_connections 1024; }
```
- Max concurrent connections **per worker**. Total ≈ `workers × 1024`.

---

## `http` block (HTTP/1.1 + HTTP/2 context)

**Performance toggles**
- `sendfile on;` — kernel zero-copy for static file sends.
- `tcp_nopush on;` — coalesce packets with `sendfile` for fewer packets.
- `tcp_nodelay on;` — flush small packets promptly (low latency APIs).
- `keepalive_timeout 65;` — how long to keep client connections open.
- `include /etc/nginx/mime.types; default_type application/octet-stream;` — content type mapping & fallback.

**Tip:** Add `server_tokens off;` to hide NGINX version in responses.

---

## Upstreams (name → backend pool)

```nginx
upstream service_a_upstream { server service_a:8080; }
upstream service_b_upstream { server service_b:8080; }
upstream grpc_upstream      { server grpc_service:8090; } # h2c
```

- `service_a`/`service_b` are container DNS names from Docker Compose (HTTP/1.1).
- `grpc_service` is your .NET gRPC service speaking **HTTP/2 cleartext (h2c)**.
- You can add load balancing parameters, for example:
  ```nginx
  upstream service_a_upstream {
    least_conn;
    server service_a:8080 max_fails=3 fail_timeout=10s;
    # server service_a_2:8080 backup;  # optional second instance
  }
  ```

**Keepalive to upstreams (connection reuse)** — lowers latency under load:
```nginx
upstream service_a_upstream {
  server service_a:8080;
  keepalive 64;                 # keep 64 idle connections to the app
}
```

Then in locations:
```nginx
proxy_http_version 1.1;
proxy_set_header Connection "";
```

---

## HTTP → HTTPS redirect server

```nginx
server {
  listen 80;
  server_name localhost;
  return 301 https://$host$request_uri;
}
```
- Redirects all HTTP traffic to HTTPS preserving **host + path + query** (`$host$request_uri`).

---

## TLS + HTTP/2 server

```nginx
server {
  listen 443 ssl http2;
  server_name localhost;

  ssl_certificate     /etc/nginx/certs/server.crt;
  ssl_certificate_key /etc/nginx/certs/server.key;
  ssl_protocols       TLSv1.2 TLSv1.3;
  ssl_ciphers         HIGH:!aNULL:!MD5;
```
- Terminates TLS and enables HTTP/2 to the client (browsers, grpcurl, Postman).
- For production hardening:
  ```nginx
  ssl_prefer_server_ciphers on;
  ssl_session_cache shared:SSL:10m;
  ssl_session_timeout 10m;
  # OCSP stapling (needs resolver and valid chain)
  # ssl_stapling on;
  # ssl_stapling_verify on;
  # resolver 1.1.1.1 8.8.8.8 valid=300s ipv6=off;
  # add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
  ```

---

## REST reverse proxy locations

### `/svc1/` → Service A
```nginx
location /svc1/ {
  proxy_set_header Host $host;
  proxy_set_header X-Forwarded-For $remote_addr;
  proxy_set_header X-Forwarded-Proto $scheme;
  proxy_set_header X-Forwarded-Prefix /svc1;
  rewrite ^/svc1/?(.*)$ /$1 break;
  proxy_pass http://service_a_upstream;
}
```
- **Headers**: preserve original host, client IP, scheme, and prefix context.
- **Rewrite**: strip `/svc1` so backend sees `/` as root.
- **proxy_pass**: HTTP/1.1 to upstream.

### `/svc2/` → Service B
```nginx
location /svc2/ {
  proxy_set_header Host $host;
  proxy_set_header X-Forwarded-For $remote_addr;
  proxy_set_header X-Forwarded-Proto $scheme;
  proxy_set_header X-Forwarded-Prefix /svc2;
  rewrite ^/svc2/?(.*)$ /$1 break;
  proxy_pass http://service_b_upstream;
}
```

**If your .NET apps need the prefix preserved**, remove the `rewrite` and adjust app routing.

---

## gRPC reverse proxy location

```nginx
location /grpc {
  grpc_set_header Host $host;
  grpc_set_header X-Forwarded-Proto $scheme;
  grpc_set_header X-Forwarded-For $remote_addr;
  grpc_pass grpc://grpc_upstream;
}
```
- Client side: **TLS + HTTP/2**.
- Backend side: **h2c** via `grpc_pass` to `.NET` gRPC.
- If backend also uses TLS, switch to `grpcs://` and configure `proxy_ssl_*`:
  ```nginx
  grpc_pass grpcs://grpc_upstream;
  proxy_ssl_server_name on;
  proxy_ssl_protocols TLSv1.2 TLSv1.3;
  proxy_ssl_trusted_certificate /etc/nginx/certs/ca.crt;  # if using private CA
  ```

**Long-lived streams / bigger messages**
```nginx
grpc_read_timeout   600s;
grpc_send_timeout   600s;
client_max_body_size 20m;
```

---

## Health probe

```nginx
location = /healthz {
  return 200 "ok";
  add_header Content-Type text/plain;
}
```
- Lightweight local endpoint that doesn’t hit upstreams.

---

# 🔥 Very Important NGINX Stanzas for Production

## 1) Microcaching (for REST GETs)
Reduce upstream load by caching short-lived responses safely.

```nginx
# define cache path (10k keys, ~100MB, 1m inactive)
proxy_cache_path /var/cache/nginx levels=1:2 keys_zone=api_cache:10m
                 inactive=1m max_size=100m use_temp_path=off;

map $request_method $cacheable_method {
  default 0;
  GET     1;
  HEAD    1;
}

server {
  # ... TLS, etc.

  # attach cache to server
  proxy_cache_key "$scheme$proxy_host$request_uri";
  add_header X-Cache-Status $upstream_cache_status always;

  location /svc1/ {
    # caching rules
    proxy_cache         api_cache;
    proxy_cache_bypass  $arg_nocache;   # allow ?nocache=1 to bypass
    proxy_no_cache      $arg_nocache;
    proxy_cache_valid   200 302 30s;
    proxy_cache_valid   404 10s;
    proxy_cache_methods GET HEAD;

    # only cache GET/HEAD
    if ($cacheable_method = 0) { set $skip_cache 1; }
    proxy_cache_bypass $skip_cache;
    proxy_no_cache     $skip_cache;

    # proxy headers + pass as above
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-Prefix /svc1;
    rewrite ^/svc1/?(.*)$ /$1 break;
    proxy_pass http://service_a_upstream;
  }
}
```
**Notes**
- Use only for **idempotent** endpoints (GET/HEAD).
- Respect origin cache headers (optional): `proxy_ignore_headers off;` by default NGINX honors upstream `Cache-Control`/`Expires`.
- Add cache busting on deploy (keys include URLs; vary by headers if needed).

## 2) Compression (gzip / brotli)
```nginx
gzip on;
gzip_comp_level 5;
gzip_min_length 1024;
gzip_types text/plain text/css application/json application/javascript application/xml;
# For brotli (if module available):
# brotli on; brotli_comp_level 5; brotli_types text/plain application/json application/javascript text/css;
```
- Don’t compress already-compressed types (images, pdf, etc.).

## 3) Request/Response size & buffers
```nginx
client_max_body_size 20m;       # allow larger uploads
client_body_buffer_size 64k;
large_client_header_buffers 4 32k;
proxy_buffers 8 32k;
proxy_busy_buffers_size 64k;
```
- Bump as needed for big headers (JWTs) or bodies.

## 4) Timeouts
```nginx
proxy_connect_timeout 5s;
proxy_send_timeout    60s;
proxy_read_timeout    60s;

grpc_read_timeout     600s;
grpc_send_timeout     600s;
keepalive_timeout     65s;
```
- Increase for server-streaming or long-running gRPC.

## 5) Logging (structured)
```nginx
log_format json escape=json
  '{ "time":"$time_iso8601", "remote_addr":"$remote_addr", "method":"$request_method", '
  '"host":"$host", "uri":"$request_uri", "status":$status, "bytes_sent":$bytes_sent, '
  '"upstream_addr":"$upstream_addr", "upstream_status":"$upstream_status", '
  '"request_time":$request_time, "upstream_time":$upstream_response_time }';

access_log /var/log/nginx/access.json json;
error_log  /var/log/nginx/error.log warn;
```
- Simplifies ingestion into ELK/Graylog or CloudWatch.

## 6) Rate Limiting (protect services)
```nginx
# define a rate limit zone by client IP
limit_req_zone $binary_remote_addr zone=api_rl:10m rate=10r/s;

server {
  # ...
  location /svc1/ {
    limit_req zone=api_rl burst=20 nodelay;
    # ... rest of proxy config
  }
}
```
- Apply selectively to sensitive routes.

## 7) Security headers (TLS site)
```nginx
add_header X-Content-Type-Options nosniff always;
add_header X-Frame-Options DENY always;
add_header Referrer-Policy no-referrer-when-downgrade always;
# add_header Content-Security-Policy "default-src 'self'" always;  # tune for your app
```

## 8) CORS (if your frontend is on another origin)
```nginx
location /svc1/ {
  if ($request_method = OPTIONS) {
    add_header Access-Control-Allow-Origin  "*" always;
    add_header Access-Control-Allow-Methods "GET, POST, PUT, PATCH, DELETE, OPTIONS" always;
    add_header Access-Control-Allow-Headers "Authorization, Content-Type" always;
    add_header Access-Control-Max-Age "86400" always;
    return 204;
  }
  add_header Access-Control-Allow-Origin "*" always;
  # ... proxy settings
}
```

---

## Common gRPC Pitfalls & Fixes

- **Client talks HTTP/1.1** → Ensure client connects over TLS+HTTP/2. For curl/grpcurl use `https://...` and don’t downgrade.
- **TLS on backend too** → Use `grpcs://` and set `proxy_ssl_*`/trusted CAs.
- **Large messages / long streams** → Increase `grpc_*_timeout` and possibly `client_max_body_size`.
- **Proxies in the path** → Any intermediate L7 proxy must be HTTP/2-aware for gRPC.

---

## TL;DR

- **80 → 443 redirect**, keep URLs intact via `https://$host$request_uri`.
- **443**: TLS + HTTP/2, proxies to:
  - `/svc1/*` → Service A (HTTP/1.1, prefix stripped)
  - `/svc2/*` → Service B (HTTP/1.1, prefix stripped)
  - `/grpc`   → gRPC upstream (h2c)
  - `/healthz` → local OK
- Add **microcaching** for GETs, **compression**, **timeouts**, **upstream keepalive**, **structured logs**, and **rate limiting** for production hardening.

---

### Appendix: Useful NGINX Variables
- `$scheme` — `http` or `https`
- `$host` — hostname from `Host` header or `server_name`
- `$request_uri` — original path + query (`/foo?x=1`)
- `$uri` — normalized URI after internal rewrites (no query)
- `$remote_addr` — client IP
- `$upstream_addr` — chosen upstream’s IP:port
- `$upstream_status` / `$status` — response codes
- `$request_time` / `$upstream_response_time` — timing metrics

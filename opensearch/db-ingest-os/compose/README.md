
# Compose Infra Components

- This infra contains, Postgres + Patroni + HAProxy + ETCD + Redis + Minio + opensearch + kafka + debezium

- Connect to Adminer localhost:8080
- server:  haproxy:5000 
- user: postgres
- password: postgres
- database: mfdb [ This would be created using db_migrate container.. if it is successfully completed, then mfdb would be available]

## make debezium call 

```powershell 
curl.exe -s -4 -X POST http://127.0.0.1:8083/connectors -H "Content-Type: application/json" --data-binary "@register-pg.json"
```
```bash
url -X POST http://127.0.0.1:18083/connectors \
  -H "Content-Type: application/json" \
  -H "Expect:" \
  --data-binary @register-pg.json
  ```
  
## make kafka to opensearch sync 
```powershell
curl.exe -s -4 -X POST http://127.0.0.1:8083/connectors -H "Content-Type: application/json" --data-binary  "@register-os-sink.json"
```

```powershell

## debug kafka for opensearch 

```bash
podman exec -it kafka bash -lc 'kafka-topics --bootstrap-server kafka:9092 --list'
```

## To check messages in kafka 
```bash
podman exec -it kafka kafka-console-consumer --bootstrap-server kafka:9092 --topic mfdb.public.scheme_data --from-beginning --max-messages 5
  ```

## To check and fetch opensearch data for a topic

```powershell
curl.exe -s -X POST "http://localhost:9200/mfdb.public.scheme_data/_refresh" | Out-Null
# replace 7 with the returned id
curl.exe "http://localhost:9200/mfdb.public.scheme_data/_doc/7?pretty"
curl.exe "http://localhost:9200/mfdb.public.scheme_data/_search?q=first_name:Jiten&pretty"
```
# MF Stack — Ports & Networking Guide

This doc explains **which ports are used by each service**, how they’re exposed **inside the Compose network** vs **on your host**, and how to **call each component** correctly.

---

## Quick concepts

- **Container port**: the port a service listens on *inside* the Compose network (service-to-service traffic).
- **Host port**: the Windows/macOS/Linux host port mapped via `ports: ["HOST:CONTAINER"]`. Use this from your laptop/browser.
- **Service DNS name**: inside the Compose network, each service is reachable at `http://<service-name>:<container-port>` thanks to Docker/Podman DNS.
- **Expose vs Ports**  
  - `expose: ["9187"]` → port visible to **other containers only** (not your host).  
  - `ports: ["19090:9090"]` → publishes the container port to your **host**.

---

## How to reach things

- **From your host (browser/curl/psql)** → `http://localhost:<HOST_PORT>`
- **From another container** → `http://<service-name>:<CONTAINER_PORT>`

**Examples**
- Kafka Connect REST (host): `http://localhost:18083/…`  
- Kafka Connect REST (container): `http://connect:8083/…`  
- PostgreSQL via HAProxy RW (container): `host=haproxy port=5000`  
- OpenSearch (host): `http://localhost:19200`  
- OpenSearch (container): `http://opensearch:9200`

---

## Port map by service

> If a service has **no `ports:`**, it is **not reachable from the host**, only from other containers.

| Service | Purpose | Host → Container Ports | In-network URL | Notes / How to call |
|---|---|---:|---|---|
| **nginx** | Reverse proxy | `51986:80` | `http://nginx:80` | Host: `http://localhost:51986/` |
| **nginx-exporter** | Prometheus exporter for NGINX | `9113:9113` | `http://nginx-exporter:9113/metrics` | Scrapes NGINX metrics from `http://nginx:8080/metrics`. |
| **haproxy** | PG RW/RO routing | `5000:5000`, `5001:5001`, `8404:8404` | `tcp://haproxy:5000` (RW), `tcp://haproxy:5001` (RO), `http://haproxy:8404/metrics` | Host RW: `psql -h localhost -p 5000 …`. |
| **etcd** | DCS for Patroni | `2379:2379` | `http://etcd:2379` | Internal; optional host debugging. |
| **patroni1** | PG node 1 | `8008:8008` (REST) | `http://patroni1:8008`, `postgres://patroni1:5432` | REST role: `curl http://localhost:8008/role`. |
| **patroni2** | PG node 2 | `8009:8008` (REST) | `http://patroni2:8008`, `postgres://patroni2:5432` | Host REST: `http://localhost:8009/role`. |
| **patroni3** | PG node 3 | `8010:8008` (REST) | `http://patroni3:8008`, `postgres://patroni3:5432` | Host REST: `http://localhost:8010/role`. |
| **adminer** | DB web UI | `18080:8080` | `http://adminer:8080` | Host: `http://localhost:18080`. Server: `haproxy`. |
| **postgres-exporter-1/2/3** | Prometheus PG exporter | *expose* `9187` | `http://postgres-exporter-1:9187/metrics` | Internal only. |
| **redis** | Cache | `6379:6379` | `redis://redis:6379` | Host: `redis-cli -h localhost -p 6379 ping`. |
| **redis-exporter** | Prometheus exporter | *expose* `9121` | `http://redis-exporter:9121/metrics` | Internal only. |
| **minio** | S3-compatible object store | `9000:9000`, `9001:9001` | `http://minio:9000`, `http://minio:9001` | Host console: `http://localhost:9001`. |
| **prometheus** | Metrics DB/UI | `19090:9090` | `http://prometheus:9090` | Host: `http://localhost:19090`. |
| **kafka** | Kafka broker | `9092:9092` | `PLAINTEXT://kafka:29092` | Internal: `kafka:29092`. Host: `localhost:9092`. |
| **kafka-ui** | Kafka UI | `18081:8080` | `http://kafka-ui:8080` | Host: `http://localhost:18081`. |
| **connect** | Kafka Connect / Debezium | `18083:8083` | `http://connect:8083` | Host: `http://localhost:18083/connectors`. |
| **opensearch** | Search/analytics | `19200:9200`, `19600:9600` | `http://opensearch:9200` | Host: `http://localhost:19200`. |
| **opensearch-dashboards** | OpenSearch UI | `15601:5601` | `http://opensearch-dashboards:5601` | Host: `http://localhost:15601`. |
| **jaeger** | Tracing | `16686`, `4317`, `4318`, `14250`, `14268`, `9411` | `http://jaeger:16686` | Host UI: `http://localhost:16686`. |
| **connector-registrar** | Registers connectors | — | — | No ports; runs script. |
| **db-migrator** | Migration runner | — | — | No ports; one-shot job. |
| **user-service** | App | `8081:8081` | `http://user-service:8081` | Host: `http://localhost:8081`. |
| **scheme-service** | App | `8084:8084` | `http://scheme-service:8084` | Host: `http://localhost:8084`. |
| **upload-service** | App | `8082:8082` | `http://upload-service:8082` | Host: `http://localhost:8082`. |
| **profile-service** | App | `8085:8085` | `http://profile-service:8085` | Host: `http://localhost:8085`. |
| **customer-service** | App | `8086:8086` | `http://customer-service:8086` | Host: `http://localhost:8086`. |
| **upload-file-service** | Worker/API | *(no ports)* | `http://upload-file-service:<port>` | Internal only. |

---

## Common call patterns

### PostgreSQL via HAProxy (leader/RW)
- **From host**
  ```bash
  psql "host=localhost port=5000 user=postgres password=postgres dbname=mfdb sslmode=require"

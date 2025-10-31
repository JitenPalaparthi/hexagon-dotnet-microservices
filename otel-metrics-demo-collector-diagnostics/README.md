# Diagnostics Build: OTLP → OTel Collector → Prometheus

This version adds:
- Background metric generator (no manual traffic needed)
- Collector logging exporter (prints every metric to `docker logs otel-collector`)
- Prometheus also scrapes **itself** for an always-UP target

## Run
```bash
docker compose up --build
# App:            http://localhost:8080
# Collector /metrics: http://localhost:9464/metrics
# Prometheus:     http://localhost:9090
```

## Smoke Tests
1) **Collector emits /metrics**
```bash
curl http://localhost:9464/metrics | head -n 30
```
You should see metric families for `demo_*` and runtime metrics.

2) **Collector receives metrics** (very verbose)
```bash
docker logs -f $(docker ps --filter name=otel-collector -q)
```
Look for "Exporting metrics" lines listing `demo_requests_total` etc.

3) **Prometheus target**  
Open http://localhost:9090/targets → `otel-collector` should be **UP**.

4) **Queries**
- `demo_requests_total`
- `demo_queue_depth`
- `demo_processing_duration_seconds_bucket`
- `process_runtime_dotnet_gc_collections_count_total`
```

# Troubleshooting quick hits
# - If target is DOWN: ensure containers are in the same network (`docker network ls` / `inspect`)
# - If collector logs show no incoming metrics: check app env `OTLP__Endpoint=http://otel-collector:4317`
# - If arm64: the chosen images support arm64.
# - Time skew: ensure system time is sane; Prom uses timestamps.


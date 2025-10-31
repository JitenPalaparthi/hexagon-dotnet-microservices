# .NET 9 Metrics via OTLP → OpenTelemetry Collector → Prometheus

This variant avoids the Prometheus ASP.NET exporter alpha by pushing metrics via **OTLP gRPC** to the **OpenTelemetry Collector**, which exposes a Prometheus endpoint. Prometheus then scrapes the collector.

### Stack
- App (net9.0, OpenTelemetry 1.8.x stable): emits Counter, Gauge, Histogram + Runtime metrics.
- OTel Collector: receives OTLP metrics and exposes **/metrics** on **:9464**.
- Prometheus: scrapes the collector at **otel-collector:9464**.

### Run
```bash
docker compose up --build
# App:            http://localhost:8080
# OTel Collector: :4317 (OTLP gRPC), :9464 (Prometheus)
# Prometheus:     http://localhost:9090
```

### Test
```bash
# Drive some traffic
curl -X POST "http://localhost:8080/enqueue?item=a"
curl "http://localhost:8080/work?ms=300"

# In Prometheus UI, try queries:
#   demo_requests_total
#   demo_queue_depth
#   demo_processing_duration_seconds_bucket
#   process_runtime_dotnet_gc_collections_count_total
```

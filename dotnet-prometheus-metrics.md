# 📊 Important .NET Runtime + Application Metrics (Prometheus + OpenTelemetry)

## 🧠 Overview
These metrics come from:
- `AddRuntimeInstrumentation()` → internal .NET CLR/GC/JIT metrics
- `AddAspNetCoreInstrumentation()` → HTTP request metrics
- Your custom `Meter` instruments (Counters, Gauges, Histograms)

Use **PromQL functions** to derive rates, averages, and latency percentiles.

---

## 1️⃣ Process & CPU Metrics

| Metric | Type | Meaning | Example Query |
|--------|------|---------|---------------|
| `process_cpu_seconds_total` | Counter | Total CPU time used by the process (user + kernel). | `rate(process_cpu_seconds_total[1m])` → CPU seconds/sec (≈ cores used) |
| `process_uptime_seconds_total` | Counter | Seconds since process started. | `max(process_uptime_seconds_total)` |
| `process_memory_bytes` | Gauge | Resident memory (working set). | `process_memory_bytes` |

🧩 **Tip:**  
If `rate(process_cpu_seconds_total[1m])` ≈ number of vCPUs, your app is saturating CPU.

---

## 2️⃣ Garbage Collection & Heap

| Metric | Type | Meaning | Example Query |
|--------|------|---------|---------------|
| `process_runtime_dotnet_gc_heap_size_bytes` | Gauge | Current managed heap size. | `process_runtime_dotnet_gc_heap_size_bytes` |
| `process_runtime_dotnet_gc_collections_count_total` | Counter | Count of GC collections by generation (gen0/gen1/gen2). | `increase(process_runtime_dotnet_gc_collections_count_total[5m])` |
| `process_runtime_dotnet_gc_allocated_bytes_total` | Counter | Total allocated bytes on managed heap. | `rate(process_runtime_dotnet_gc_allocated_bytes_total[1m])` |

🧩 **Tip:**  
A high allocation rate + frequent GCs = potential memory churn or object retention issues.

---

## 3️⃣ Thread Pool & Tasks

| Metric | Type | Meaning | Example Query |
|--------|------|---------|---------------|
| `process_runtime_dotnet_threadpool_threads_count` | Gauge | Current number of thread pool threads. | `avg(process_runtime_dotnet_threadpool_threads_count)` |
| `process_runtime_dotnet_threadpool_completed_items_count_total` | Counter | Total work items processed by thread pool. | `rate(process_runtime_dotnet_threadpool_completed_items_count_total[1m])` |

🧩 **Tip:**  
If thread count grows without a rise in throughput, you may have blocking I/O or contention.

---

## 4️⃣ Exceptions & JIT

| Metric | Type | Meaning | Example Query |
|--------|------|---------|---------------|
| `process_runtime_dotnet_exceptions_total` | Counter | Total exceptions thrown. | `increase(process_runtime_dotnet_exceptions_total[1m])` |
| `process_runtime_dotnet_jit_methods_compiled_total` | Counter | Methods JIT-compiled since process start. | `increase(process_runtime_dotnet_jit_methods_compiled_total[5m])` |

🧩 **Tip:**  
A spike in exceptions usually correlates with failed requests or poor error handling.

---

## 5️⃣ ASP.NET Core HTTP Metrics

| Metric | Type | Meaning | Example Query |
|--------|------|---------|---------------|
| `http_server_request_duration_seconds_bucket` | Histogram | Request latency buckets. | `histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))` |
| `http_server_active_requests` | Gauge | Requests currently in progress. | `avg(http_server_active_requests)` |
| `http_server_requests_total` | Counter | Total HTTP requests processed. | `increase(http_server_requests_total[1m])` |

🧩 **Tip:**  
`histogram_quantile(0.95, …)` gives **95th percentile latency** — great for SLO dashboards.

---

## 6️⃣ Custom Metrics (Example)

| Metric | Type | Description | Useful Query |
|--------|------|--------------|---------------|
| `demo_requests_total` | Counter | Custom counter for operations. | `increase(demo_requests_total[1m])` |
| `demo_queue_depth` | Gauge | Queue length. | `avg(demo_queue_depth)` |
| `demo_processing_duration_seconds_bucket` | Histogram | Work duration distribution. | `histogram_quantile(0.9, sum(rate(demo_processing_duration_seconds_bucket[5m])) by (le))` |

---

## 🔢 Common PromQL Functions

| Function | Use Case | Example |
|-----------|-----------|----------|
| `rate(counter[1m])` | Per-second rate of counter increase. | `rate(http_server_requests_total[1m])` |
| `increase(counter[5m])` | Total increment over 5m window. | `increase(process_runtime_dotnet_gc_collections_count_total[5m])` |
| `avg_over_time(gauge[5m])` | Average gauge value over time. | `avg_over_time(process_memory_bytes[5m])` |
| `max_over_time(gauge[5m])` | Max gauge value in window. | `max_over_time(demo_queue_depth[5m])` |
| `histogram_quantile(0.95, …)` | Percentiles from histogram buckets. | `histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))` |
| `sum(...) by (label)` | Aggregate across labels. | `sum(rate(http_server_requests_total[1m])) by (status_code)` |

---

## 🧭 Typical Dashboard KPIs

| Area | Metric | Goal / Alert Threshold |
|------|---------|-------------------------|
| CPU | `rate(process_cpu_seconds_total[1m])` | > 80% sustained → investigate |
| GC | `increase(process_runtime_dotnet_gc_collections_count_total[5m])` | High gen2 frequency → memory tuning |
| Memory | `process_runtime_dotnet_gc_heap_size_bytes` | Trending upward steadily → leak suspicion |
| HTTP | `histogram_quantile(0.95, …)` | > 1s → latency regression |
| Errors | `increase(process_runtime_dotnet_exceptions_total[1m])` | Sudden spikes → app bug or upstream errors |

---

## 🧩 How to Extend
Add new custom metrics via:

```csharp
var meter = new Meter("MyApp.Metrics");
var cacheHits = meter.CreateCounter<long>("cache_hits_total");
var latency = meter.CreateHistogram<double>("db_query_duration_seconds");
```

They will automatically appear in Prometheus under the same pipeline.

---

### ✅ Summary
- **Runtime metrics** → health of CLR, GC, CPU, threads
- **Application metrics** → performance and business ops
- **PromQL functions** let you convert raw counters/gauges/histograms into trends and percentiles

Combine both sets in Grafana for a complete observability picture.

# 📘 Azure Service Bus Consumer Tuning — `PROCESSOR__MAXCONCURRENT` and `PROCESSOR__PREFETCH`

## ⚙️ Overview

These two settings control **how your Azure Service Bus consumer** (the Worker Service) receives and processes messages in parallel and how many messages it keeps **prefetched in memory** for performance.

| Setting | SDK Mapping | Purpose |
|----------|--------------|----------|
| `PROCESSOR__MAXCONCURRENT` | `ServiceBusProcessorOptions.MaxConcurrentCalls` | Number of messages processed *at the same time* |
| `PROCESSOR__PREFETCH` | `ServiceBusProcessorOptions.PrefetchCount` | Number of messages fetched and buffered ahead of time |

---

## 🧩 1. `PROCESSOR__MAXCONCURRENT`

**Meaning:**  
How many messages can be processed **in parallel** by this consumer instance.

**Analogy:**  
> “How many checkout counters are open at once?”

**Example:**
```env
PROCESSOR__MAXCONCURRENT=8
```

Your consumer can now handle **8 messages concurrently**.  
Each message handler (`ProcessMessageAsync`) runs in parallel up to this limit.

**Example Behavior (16 messages):**

| Setting | Behavior | Total Time (if each message = 5s) |
|----------|-----------|-----------------------------------|
| `MAXCONCURRENT=1` | Sequential | ~80s |
| `MAXCONCURRENT=8` | 8 messages processed together | ~10s |

**When to Increase**
- Your processing is **I/O-bound** (DB writes, HTTP calls, etc.).
- You want higher throughput and have multiple CPU cores.

**When to Decrease**
- Your work is **CPU-bound** or shares resources (locks, DB rows, etc.).
- You want simpler, predictable logs or ordering.

---

## 🧩 2. `PROCESSOR__PREFETCH`

**Meaning:**  
How many messages the SDK fetches from Service Bus **in advance** and holds in memory.

**Analogy:**  
> “How many customers are waiting in line at each checkout counter?”

**Example:**
```env
PROCESSOR__PREFETCH=50
```

The client library requests **50 messages ahead of time**, buffering them locally.  
When a worker thread finishes one message, another is immediately available — reducing network latency.

**Behavior Comparison:**

| Setting | Fetch Style | Effect |
|----------|--------------|--------|
| `PREFETCH=0` | Pull each message one by one | Lowest memory, highest latency |
| `PREFETCH=8` | Keeps small buffer ready | Balanced |
| `PREFETCH=50` | Large buffer, instant availability | Highest throughput, more memory |

---

## 🧩 Combined Behavior

Example:
```env
PROCESSOR__MAXCONCURRENT=8
PROCESSOR__PREFETCH=50
```

- 8 messages processed simultaneously  
- 50 messages buffered locally  
- As soon as one finishes, the next message starts instantly — no network delay.

---

## 🧪 Demo Test

1. **Send 100 messages:**
   ```bash
   curl -X POST "http://localhost:8080/send?count=100" \
        -H "content-type: application/json" \
        -d '{"text":"demo"}'
   ```

2. **Run consumer (slow):**
   ```bash
   export PROCESSOR__MAXCONCURRENT=1
   export PROCESSOR__PREFETCH=0
   dotnet run --project ./consumer
   ```

3. **Run consumer (fast):**
   ```bash
   export PROCESSOR__MAXCONCURRENT=8
   export PROCESSOR__PREFETCH=50
   dotnet run --project ./consumer
   ```

**Observe:**  
- With concurrency = 1 → sequential, slow  
- With concurrency = 8, prefetch = 50 → bursts of messages, much faster throughput

---

## ✅ Quick Reference

| Setting | Controls | Typical Range | Impact |
|----------|-----------|----------------|--------|
| `PROCESSOR__MAXCONCURRENT` | Active parallel handlers | 1–16 | More parallelism = more CPU & throughput |
| `PROCESSOR__PREFETCH` | Buffered messages in memory | 10–200 | More prefetch = lower latency, more memory |

---

## 🧠 Summary Analogy

| Concept | Analogy | Effect |
|----------|----------|--------|
| `MAXCONCURRENT` | Number of checkout counters | Controls concurrency |
| `PREFETCH` | Number of people waiting in each line | Controls buffering & latency |

---

## 💡 Tip for Monitoring

You can expose metrics like:
- Active messages being processed
- Prefetched messages remaining
- Throughput (messages per second)

Using **Prometheus + OpenTelemetry**, you can visualize the impact of these two parameters in Grafana.

---

**Author:** *Jiten Palaparthi — Azure Service Bus Deep Dive, .NET 9 Edition*  
**Version:** 1.0 — Optimizing Message Throughput with Prefetch & Concurrency  

# Azure Service Bus **Pub/Sub with Sessions + DLQ** — .NET 9 Demo (Emulator)

This demo shows **pub/sub** using a **Topic** with **two Subscriptions** (`sub-a`, `sub-b`), each **session-enabled** for **per-order FIFO**, and **DLQ** handling.
Runs with the **Service Bus Emulator** in Docker; apps run on host (macOS/Windows).

## Topology
```
Producer --> Topic: demo-topic --> [Subscription: sub-a] --> Consumer A (session processor, FIFO)
                                └-> [Subscription: sub-b] --> Consumer B (session processor, FIFO)
                                               (each has its own DLQ)
```

- **Sessions** preserve ordering per `orderId` (SessionId).
- **Multiple subscribers** each receive a **copy** of every message.
- **DLQ**: MaxDeliveryCount=3, TTL=30m, DLQ on expiration enabled.

Pinned to **.NET SDK 9.0.306** via `global.json`.

> ⚠️ On macOS/Windows: **run apps on host**. Emulator enforces `Endpoint=sb://localhost` and does not accept Docker‑namespace clients.

---

## 0) Start emulator

```bash
cp .env.example .env
docker compose up -d
docker logs -f servicebus-emulator   # wait until ready
```

## 1) Environment for apps (host)
```bash
export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export TOPIC__NAME="demo-topic"
```

## 2) Run Producer
```bash
dotnet run --framework net9.0 --project ./producer
# health
curl http://localhost:8080/healthz
```

## 3) Run two subscribers (sub-a and sub-b)

### Subscriber A (sub-a)
```bash
export SUBSCRIPTION__NAME=sub-a
export MAX_CONCURRENT_SESSIONS=4
export PREFETCH=50
# Optional: simulate failure at step to trigger DLQ in sub-a only
export SIM_FAIL_STEP=2
dotnet run --framework net9.0 --project ./consumer
```

### Subscriber B (sub-b)
Open a new terminal:
```bash
export SUBSCRIPTION__NAME=sub-b
export MAX_CONCURRENT_SESSIONS=4
export PREFETCH=50
# No failure here
unset SIM_FAIL_STEP
dotnet run --framework net9.0 --project ./consumer
```

## 4) Publish ordered messages (per orderId)
```bash
# one order
curl -X POST "http://localhost:8080/send-order?orderId=order-123&steps=3&repeat=1"

# multiple orders
curl -X POST "http://localhost:8080/send-order?orderId=order-ABC&steps=3&repeat=1"
curl -X POST "http://localhost:8080/send-order?orderId=order-XYZ&steps=4&repeat=2"
```

- Both **sub-a** and **sub-b** receive all messages.
- Each `orderId` is a **SessionId**; messages are **FIFO** per session.
- If `SIM_FAIL_STEP=2` on sub-a, only sub‑a’s consumer DLQs step=2; sub‑b processes normally.

## 5) Read Dead‑Letter messages (per subscription)

### Read sub-a DLQ
```bash
export SUBSCRIPTION__NAME=sub-a
dotnet run --framework net9.0 --project ./dlqreader
```

### Read & requeue sub-a DLQ
```bash
export SUBSCRIPTION__NAME=sub-a
export DLQ_REQUEUE=1
dotnet run --framework net9.0 --project ./dlqreader
```

(For sub‑b, set `SUBSCRIPTION__NAME=sub-b`.)

## 6) Stop
```bash
docker compose down -v
```

---

### Notes
- **Ordering**: `ServiceBusSessionProcessor` with `MaxConcurrentCallsPerSession=1` and `SessionId=orderId` on messages.
- **Scaling**: Increase `MAX_CONCURRENT_SESSIONS` to process many sessions in parallel.
- **DLQ**: Exceeding `MaxDeliveryCount=3`, explicit dead‑letter, or TTL expiry (`DeadLetteringOnMessageExpiration=true`).

Happy pub/sub testing!

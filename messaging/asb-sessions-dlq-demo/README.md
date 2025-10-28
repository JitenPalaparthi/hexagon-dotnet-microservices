# Azure Service Bus **Sessions + DLQ** — .NET 9 Demo (Emulator)

End‑to‑end demo showing:
- **Ordered (FIFO) processing** per `orderId` using **Sessions**
- **Dead‑Letter Queue (DLQ)** on failures (MaxDeliveryCount or explicit dead‑letter)
- Local development with **Service Bus Emulator** in Docker (apps run on host)

## Contents
- `docker-compose.yml` — Emulator + SQL (with healthcheck)
- `emulator/config.json` — `orders-queue` (RequiresSession=true, MaxDeliveryCount=3, TTL=30m, DLQ on expiration)
- `producer` (Minimal API): `POST /send-order?orderId=...&steps=3&repeat=1`
- `consumer` (Worker): Session processor, ordered; can simulate failure on a given step
- `dlqreader` (Console): Reads from `orders-queue/$DeadLetterQueue` and prints messages; can requeue

Pinned to **.NET SDK 9.0.306** via `global.json`.

> ⚠️ On macOS/Windows: **run apps on host**. Emulator enforces `Endpoint=sb://localhost` and does not accept Docker‑namespace clients.

---

## 0) Start emulator

```bash
cp .env.example .env
docker compose up -d
# wait until logs show emulator is running and 5672 is open
docker logs -f servicebus-emulator
```

## 1) Environment for apps (host)
```bash
export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export QUEUE__NAME="orders-queue"
```

## 2) Run Producer
```bash
dotnet run --framework net9.0 --project ./producer
# health
curl http://localhost:8080/healthz
```

## 3) Run Consumer (ordered sessions)
Default: processes multiple sessions concurrently, preserves FIFO **within** each session.
```bash
export MAX_CONCURRENT_SESSIONS=4
export PREFETCH=50
# Optional: simulate failure at a step to trigger DLQ
export SIM_FAIL_STEP=2     # set to a step number or leave empty for no failure
dotnet run --framework net9.0 --project ./consumer
```

## 4) Publish ordered messages
```bash
# one order
curl -X POST "http://localhost:8080/send-order?orderId=order-123&steps=3&repeat=1"

# multiple orders
curl -X POST "http://localhost:8080/send-order?orderId=order-ABC&steps=3&repeat=1"
curl -X POST "http://localhost:8080/send-order?orderId=order-XYZ&steps=4&repeat=2"
```

- With `SIM_FAIL_STEP=2`, messages with `step=2` will **fail**, retried up to **MaxDeliveryCount=3**, then moved to **DLQ**.
- Remove `SIM_FAIL_STEP` (unset) to process successfully (no DLQ).

## 5) Read Dead‑Letter messages
```bash
# Just read and complete (drain)
dotnet run --framework net9.0 --project ./dlqreader

# Requeue DLQ messages back to active queue (for reprocessing)
export DLQ_REQUEUE=1
dotnet run --framework net9.0 --project ./dlqreader
```

### Expected DLQReader output
```
💀 DLQ MessageId=order-123-0-2-<guid>
Reason=MaxDeliveryCountExceeded  Description=Message was dead-lettered after exceeding max deliveries
SessionId=order-123  Body={"orderId":"order-123","step":2,"text":"Step2"}
```

## 6) Stop
```bash
docker compose down -v
```

---

### Notes
- **Ordering**: Achieved via `ServiceBusSessionProcessor` with `MaxConcurrentCallsPerSession=1` and setting `SessionId=orderId` on messages.
- **Scaling**: Increase `MAX_CONCURRENT_SESSIONS` to process many `orderId`s in parallel across consumers.
- **DLQ triggers**: Exceeding `MaxDeliveryCount` (here 3), explicit `DeadLetterMessageAsync`, or TTL expiry (we enabled `DeadLetteringOnMessageExpiration`).

Happy testing!

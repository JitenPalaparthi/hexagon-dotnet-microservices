# Azure Service Bus **Ordered Messages** (Sessions) — .NET 9 Drop‑in Demo

This demo shows **FIFO per key** using **Service Bus Sessions**. All messages for the **same `orderId`** are processed **in order**, while different `orderId`s can run **in parallel** across consumers.

- Emulator via Docker (`docker-compose.yml`)
- Producer: ASP.NET Core Minimal API (`/send-order?orderId=...&steps=3&repeat=1`)
- Consumer: Worker using **ServiceBusSessionProcessor** (FIFO per session)
- Queue: `orders-queue` with **RequiresSession=true**
- .NET SDK pinned to **9.0.306** (`global.json`)

## 0) Start the emulator

```bash
cp .env.example .env
docker compose up -d
```

> macOS/Windows: run **apps on host** (not in containers). The emulator enforces `Endpoint=sb://localhost` and only accepts local clients.

## 1) Run Producer & Consumers (host)

Set the connection string to point to the **local emulator**:

```bash
export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export QUEUE__NAME="orders-queue"
```

### Producer
```bash
dotnet run --framework net9.0 --project ./producer
# health check
curl http://localhost:8080/healthz
```

### Consumer A
```bash
export MAX_CONCURRENT_SESSIONS=4
export PREFETCH=50
dotnet run --framework net9.0 --project ./consumer
```

### Consumer B (optional second instance to scale out)
Open a new terminal and run the same consumer command again.
Service Bus will **distribute different sessions (orderIds)** to different consumers automatically.

## 2) Send ordered messages

Send 3 ordered events for a single order:
```bash
curl -X POST "http://localhost:8080/send-order?orderId=order-123&steps=3&repeat=1"
```

Send multiple orders:
```bash
curl -X POST "http://localhost:8080/send-order?orderId=order-ABC&steps=3&repeat=1"
curl -X POST "http://localhost:8080/send-order?orderId=order-XYZ&steps=4&repeat=2"
```

- `steps`: number of sequential steps per order (1..N). Producer sends messages `step=1..steps` in order.
- `repeat`: how many times to publish the sequence (fire multiple sequences for load).

## What to expect

- **Within a given `orderId`**, consumer logs show messages in strict **step order** (`1 → 2 → 3 → ...`).  
- With multiple consumers, **different `orderId`s** (sessions) are processed **in parallel**, but each `orderId` remains FIFO.

## Tuning

- Consumer uses `ServiceBusSessionProcessorOptions`:
  - `MaxConcurrentSessions` (env: `MAX_CONCURRENT_SESSIONS`, default 4)
  - `MaxConcurrentCallsPerSession=1` to **preserve order**
  - `PrefetchCount` (env: `PREFETCH`, default 50)

## Shut down

```bash
docker compose down -v
```

---

Made for local dev & teaching. Enjoy ordered messaging!

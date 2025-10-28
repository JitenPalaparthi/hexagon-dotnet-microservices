# Azure Service Bus **Emulator** Dev — .NET 9 (Docker Compose)

This is a ready-to-run local dev stack using the **Azure Service Bus Emulator** (Docker) with a .NET 9 **Producer** (Minimal API) and **Consumer** (Worker).

- SDK pinned to **9.0.306** via `global.json`
- Uses emulator connection string with `UseDevelopmentEmulator=true`
- **No runtime admin create**; entities are declared in `emulator/config.json`

## Topology

```
[Producer API] --(AMQP 5672)--> [Service Bus Emulator (queue: demo-queue)] --> [Consumer Worker]
```

## Quick start

```bash
cp .env.example .env
docker compose up --build

export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export QUEUE__NAME="demo-queue"

cd producer
dotnet run --framework net9.0

export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export QUEUE__NAME="demo-queue"

cd consumer
dotnet run --framework net9.0


# send test messages
curl -X POST http://localhost:5000/send -H "content-type: application/json" -d '{"text":"hello emulator"}'
curl -X POST "http://localhost:5000/send?count=5" -H "content-type: application/json" -d '{"text":"hello batch"}'
```

The consumer logs will show message processing.

## Notes

- Emulator limitations (summary): no on-the-fly management ops, no partitioned entities, AMQP-over-WebSockets not supported. Use AMQP/TCP 5672. (See Microsoft Learn docs.)
- Edit `emulator/config.json` to change queues/topics/subscriptions. Restart the emulator container after changes.
- Producer and Consumer read `SERVICEBUS__CONNECTIONSTRING` from `.env` and target `QUEUE__NAME=demo-queue`.

## Clean up

```bash
docker compose down -v
```

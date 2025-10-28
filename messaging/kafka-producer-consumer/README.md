# .NET 9 + Kafka (Confluent KRaft) — Producer & Consumer

This version uses **Confluent cp-kafka:7.7.0** with **KRaft** only. It generates the KRaft config from env,
formats storage with that generated file, and then starts the broker.

## Run

```bash
docker compose up --build
```

Produce a message:

```bash
curl -X POST http://localhost:8080/produce   -H 'Content-Type: application/json'   -d '{"text":"Hello from .NET 9 via Confluent KRaft!"}'
```

You should see the consumer log the message.

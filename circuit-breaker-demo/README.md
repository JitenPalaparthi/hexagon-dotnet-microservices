# .NET 9 Circuit Breaker Demo (Polly v8)

Two services:
- **Unstable** (port 8081): randomly fails or delays.
- **Gateway** (port 8080): calls Unstable using **Polly v8** with **Retry** + **Circuit Breaker**.

## Run
```bash
docker compose up --build
# Gateway:   http://localhost:8080
# Unstable:  http://localhost:8081
```

## Try it
```bash
# See breaker state
curl http://localhost:8080/cb/state

# Drive traffic (some calls will fail; breaker may open)
watch -n 0.5 curl -s http://localhost:8080/call | jq
```

When enough failures occur (>=50% within 30s, with at least 8 requests), the breaker **opens for 10s**, then transitions to **half-open** and allows trial calls before closing again.

## Tuning
Environment variables for **Unstable**:
- `FAILURE_RATE` (0..1, default 0.5) – probability of 500s
- `MIN_MS_DELAY`/`MAX_MS_DELAY` – random delay range
- `HARD_FAIL=true` – force 500s every time

Breaker/Retry (Gateway `Program.cs`):
- Breaker: `FailureRatio=0.5`, `MinimumThroughput=8`, `SamplingDuration=30s`, `BreakDuration=10s`
- Retry: 3 attempts, exponential backoff starting at 200ms + jitter
- Handles: timeouts, `HttpRequestException`, and `5xx` statuses

## Endpoints
- `GET /cb/state` – returns the current breaker state (closed/open/half-open)
- `GET /call` – calls `Unstable /api/unstable` through the resilience pipeline
- `GET /` – quick info
- `Unstable: GET /api/unstable` – returns 200 OK with random delay or 500 based on `FAILURE_RATE`

## Notes
- Uses only the **Polly** package (v8). No extra HttpClient integration package needed.
- Target framework: **net9.0**.

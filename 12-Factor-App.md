# 🧱 The Twelve-Factor App — Explained

> A methodology for building **scalable, maintainable, and cloud-native** software as a service (SaaS).

---

## 1️⃣ Codebase — *One Codebase, One App*
- **Rule:** One codebase tracked in version control (e.g., Git) with multiple deploys (dev, staging, prod).  
- **Why:** Keeps environments consistent and traceable.  
- **Example:**  
  ```
  myapp/
    ├── src/
    ├── tests/
    └── Dockerfile
  ```

---

## 2️⃣ Dependencies — *Explicitly Declare and Isolate*
- **Rule:** Never rely on system-wide packages; list all dependencies explicitly.  
- **Why:** Ensures repeatable builds and environment consistency.  
- **Example:**  
  - .NET → `*.csproj` lists `<PackageReference>`.  
  - Node.js → `package.json`.  
  - Python → `requirements.txt`.

---

## 3️⃣ Config — *Store Config in the Environment*
- **Rule:** Configuration (DB strings, API keys, credentials) should be externalized via environment variables, **not** hardcoded.  
- **Why:** Same code → different config per environment.  
- **Example:**  
  ```bash
  export DATABASE_URL="postgres://user:pass@host/db"
  export CONSUL_HTTP_ADDR="http://consul:8500"
  ```

---

## 4️⃣ Backing Services — *Treat as Attached Resources*
- **Rule:** Databases, message queues, caches, etc., are external resources that can be swapped without code changes.  
- **Why:** Enables scaling, portability, and resilience.  
- **Example:** Switch from local Postgres to managed RDS by just changing the connection string.

---

## 5️⃣ Build, Release, Run — *Strict Separation*
- **Rule:** Separate stages:  
  1. **Build** (compile code)  
  2. **Release** (combine build + config)  
  3. **Run** (execute in environment)  
- **Why:** Guarantees immutability and repeatability.  
- **Example:**  
  ```bash
  docker build -t myapp:v1 .
  docker run -e ASPNETCORE_ENVIRONMENT=Production myapp:v1
  ```

---

## 6️⃣ Processes — *Execute the App as One or More Stateless Processes*
- **Rule:** App instances are **stateless**; any state should go to backing services (DB, cache, etc.).  
- **Why:** Enables horizontal scaling and resilience.  
- **Example:**  
  - OK: Store session data in Redis.  
  - ❌ Bad: Store sessions in local memory.

---

## 7️⃣ Port Binding — *Export Services via Port Binding*
- **Rule:** The app should be self-contained and expose HTTP (or similar) via a port.  
- **Why:** No need for external web servers (e.g., Apache).  
- **Example:**  
  ```bash
  ASPNETCORE_URLS=http://+:8080
  dotnet run
  ```
  The app now listens on port 8080 directly.

---

## 8️⃣ Concurrency — *Scale Out via Process Model*
- **Rule:** Scale horizontally (by adding processes/containers), not vertically (by adding threads).  
- **Why:** Enables elastic scaling.  
- **Example:**  
  ```bash
  docker compose up --scale web=4
  ```

---

## 9️⃣ Disposability — *Fast Startup and Graceful Shutdown*
- **Rule:** Processes start and stop quickly and handle signals cleanly.  
- **Why:** Enables rolling updates, graceful deploys, and resilience.  
- **Example:**  
  - Handle `SIGTERM` in .NET with `IHostApplicationLifetime`.  
  - Use readiness/liveness probes in Kubernetes.

---

## 🔟 Dev/Prod Parity — *Keep Environments as Similar as Possible*
- **Rule:** Minimize the gap between development, staging, and production.  
- **Why:** Avoid "works on my machine" issues.  
- **Example:**  
  - Use Docker Compose locally that mirrors prod images.  
  - Same Consul/Redis/Postgres versions across environments.

---

## 11️⃣ Logs — *Treat Logs as Event Streams*
- **Rule:** The app should write logs to `stdout/stderr`. Aggregation happens externally.  
- **Why:** Lets platforms (Docker, Kubernetes, ELK) handle storage, rotation, and viewing.  
- **Example:**  
  ```csharp
  builder.Logging.AddConsole();
  ```
  Kubernetes or `docker logs` will capture the output.

---

## 12️⃣ Admin Processes — *Run Admin Tasks as One-Off Processes*
- **Rule:** One-off tasks (migrations, scripts) should use the same codebase and environment as the app.  
- **Why:** Avoids version drift between admin tools and main app.  
- **Example:**  
  ```bash
  docker compose run orders dotnet ef database update
  ```

---

## ✅ Summary Table

| Factor | Principle | Goal |
|---------|------------|------|
| 1. Codebase | One repo per app | Consistency |
| 2. Dependencies | Explicit declarations | Repeatability |
| 3. Config | Environment variables | Flexibility |
| 4. Backing Services | Treat as external | Portability |
| 5. Build/Release/Run | Separation | Reproducibility |
| 6. Processes | Stateless | Scalability |
| 7. Port Binding | Self-contained | Simplicity |
| 8. Concurrency | Process model | Horizontal scale |
| 9. Disposability | Fast start/stop | Resilience |
| 10. Dev/Prod Parity | Similar envs | Reliability |
| 11. Logs | Streamed to stdout | Observability |
| 12. Admin Processes | One-off scripts | Maintainability |

---

## 🧩 12-Factor in Modern .NET Microservices

| Factor | .NET Example |
|--------|---------------|
| Dependencies | NuGet packages in `.csproj` |
| Config | `IConfiguration` with `EnvironmentVariables` |
| Processes | `IHostedService` background workers |
| Port Binding | `ASPNETCORE_URLS` |
| Logs | `ILogger<T>` → Console / Serilog / OpenTelemetry |
| Admin | `dotnet ef`, `dotnet run --project Tools` |

# .NET 9 gRPC Demo (Server + Client)

This is a minimal, production-friendly gRPC demo built on **.NET 9** showing:
- An ASP.NET Core gRPC **server** (HTTP/2, cleartext h2c on localhost:5000).
- A C# **client** that calls both a unary RPC and a server-streaming RPC.

## Prereqs
- .NET 9 SDK installed (`dotnet --version` should start with `9.`).

## Project layout
```
dotnet9-grpc-demo/
  GrpcServer/
    Protos/greeter.proto
    GrpcServer.csproj
    Program.cs
  GrpcClient/
    Protos/greeter.proto (linked from server)
    GrpcClient.csproj
    Program.cs
```

## Build & Run

### 1) Run the server
```bash
cd GrpcServer
dotnet run
```
The server listens on `http://localhost:5000` (HTTP/2, h2c). You should see logs indicating Kestrel bound to port 5000.

> Note: We intentionally use **h2c** (HTTP/2 without TLS) to avoid dev-cert hassles. For production, use HTTPS/TLS.

### 2) Run the client in another terminal
```bash
cd GrpcClient
dotnet run -- http://localhost:5000 YourName
```
You should see output similar to:
```
Connecting to http://localhost:5000 ...
Unary: Hello, YourName! 👋 (from .NET X.Y.Z)
Server streaming:
  [1/5] Hello, YourName!
  ...
  [5/5] Hello, YourName!
Done.
```

## Notes
- **No `using var builder`** on `HostApplicationBuilder` — it's not `IDisposable`. The sample uses the modern `WebApplication` style (no `using` needed).
- Server includes **gRPC Reflection** in Development so you can explore with tools like `grpcui` or `grpcurl`.
- The client enables h2c via:
  ```csharp
  AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
  ```
  so `GrpcChannel.ForAddress("http://...")` works.

## Switch to HTTPS/TLS (optional)
If you prefer HTTPS, remove the custom Kestrel listen block and rely on defaults (which use dev certs). Then change the client address to `https://localhost:5xxx` (whatever the server prints) and **remove** the h2c AppContext switch.

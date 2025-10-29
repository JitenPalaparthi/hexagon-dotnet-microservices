# .NET 9 gRPC Patterns Demo

Demonstrates:
1) Unary (request-reply)
2) Server streaming (half duplex server->client)
3) Client streaming (half duplex client->server)
4) Bidirectional streaming (full duplex)

## Run
Server:
  cd PatternsServer
  dotnet run

Client:
  cd PatternsClient
  dotnet run -- http://localhost:5000

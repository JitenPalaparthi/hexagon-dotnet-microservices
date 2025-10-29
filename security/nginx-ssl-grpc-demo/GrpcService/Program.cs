using Demo.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Force HTTP/2 (h2c) for gRPC behind NGINX (TLS terminated at NGINX)
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(8090, o => o.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<GreeterService>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

app.MapGet("/", () => "gRPC service is running (h2c on 8090).");
app.Run();

public sealed class GreeterService : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        => Task.FromResult(new HelloReply { Message = $"Hello, {request.Name}! From gRPC via NGINX." });
}

using System;
using System.Threading.Tasks;
using Demo.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Kestrel: listen on HTTP/2 (h2c) without TLS for simplicity
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<GreeterService>();

// enable gRPC reflection in Development
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

// Minimal root to help humans
app.MapGet("/", () => "gRPC server is running. Use a gRPC client to talk to it.");

app.Run();

public sealed class GreeterService : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        var msg = $"Hello, {request.Name}! 👋 (from .NET {Environment.Version})";
        return Task.FromResult(new HelloReply { Message = msg });
    }

    public override async Task GreetManyTimes(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        for (var i = 1; i <= 5; i++)
        {
            await responseStream.WriteAsync(new HelloReply { Message = $"[{i}/5] Hello, {request.Name}!" });
            await Task.Delay(400, context.CancellationToken);
        }
    }
}

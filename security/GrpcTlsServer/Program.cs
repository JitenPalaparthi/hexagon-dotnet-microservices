using GrpcTlsServer.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configurable via env (optional)
var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var p) ? p : 5001;
var certPath = Environment.GetEnvironmentVariable("TLS_PFX_PATH") ?? "certs/localhost.pfx";
var certPwd  = Environment.GetEnvironmentVariable("TLS_PFX_PASSWORD") ?? "changeit";

// Kestrel: HTTPS + HTTP/2 (required for gRPC over TLS)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port, listen =>
    {
        listen.Protocols = HttpProtocols.Http2;
        listen.UseHttps(certPath, certPwd);
    });
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<GreeterService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

// Optional: quick GET to verify the process is up
app.MapGet("/", () => $"gRPC TLS server listening on https://localhost:{port} (HTTP/2)");

app.Run();
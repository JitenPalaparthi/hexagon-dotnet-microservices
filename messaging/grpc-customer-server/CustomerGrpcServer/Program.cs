using Customer.Grpc;
using CustomerGrpcServer.Data;
using CustomerGrpcServer.Services;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// HTTP/2 h2c on :5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var cs = builder.Configuration.GetConnectionString("Postgres")
         ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
         ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=customerdb";

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGrpcService<CustomerService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

app.MapGet("/", () => "Customer gRPC server running on http://localhost:5000 (h2c).");

app.Run();

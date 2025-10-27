using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ServiceDefaults;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Consul config + DI
builder.Services.Configure<ConsulOptions>(o =>
    o.Address = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? "http://consul:8500"); // in Docker, consul is 'consul'
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ConsulDiscoveryClient>();

// Register the gateway itself into Consul
builder.Services.AddHostedService<ConsulRegistrationHostedService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Identity for Consul registration (Docker defaults; env overrides win)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Environment.SetEnvironmentVariable("SERVICE_NAME", Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "apigateway");
Environment.SetEnvironmentVariable("SERVICE_HOST", Environment.GetEnvironmentVariable("SERVICE_HOST") ?? "gateway");
Environment.SetEnvironmentVariable("SERVICE_PORT", Environment.GetEnvironmentVariable("SERVICE_PORT") ?? port);

// ---- YARP initial in-memory config (use static holder so nested class can read) ----
builder.Services.AddReverseProxy().LoadFromMemory(GatewayConfig.Routes, GatewayConfig.EmptyClusters);

// Poll Consul and update clusters at runtime
builder.Services.AddHostedService<ConsulYarpUpdater>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health endpoint for Consul
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "apigateway" }));

// Just a root ping
app.MapGet("/", () => Results.Ok(new { ok = true, routes = new[] { "/catalog", "/orders" } }));

app.MapReverseProxy();

app.Run();

/// <summary>Static holder for route definitions accessible from nested classes.</summary>
static class GatewayConfig
{
    public static readonly RouteConfig[] Routes =
    {
        new RouteConfig
        {
            RouteId = "catalog",
            ClusterId = "catalogCluster",
            Match = new RouteMatch { Path = "/catalog/{**catch-all}" },
            Transforms = new[] { new Dictionary<string,string> { ["PathRemovePrefix"] = "/catalog" } }
        },
        new RouteConfig
        {
            RouteId = "orders",
            ClusterId = "ordersCluster",
            Match = new RouteMatch { Path = "/orders/{**catch-all}" },
            Transforms = new[] { new Dictionary<string,string> { ["PathRemovePrefix"] = "/orders" } }
        }
    };

    // Start with empty clusters; the updater will populate Destinations from Consul
    public static readonly ClusterConfig[] EmptyClusters =
    {
        new ClusterConfig { ClusterId = "catalogCluster", Destinations = new Dictionary<string,DestinationConfig>() },
        new ClusterConfig { ClusterId = "ordersCluster",  Destinations = new Dictionary<string,DestinationConfig>() }
    };
}

sealed class ConsulYarpUpdater(IProxyConfigProvider provider, ConsulDiscoveryClient consul, ILogger<ConsulYarpUpdater> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mem = (InMemoryConfigProvider)provider;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cat = await consul.GetHealthyInstancesAsync("catalog", stoppingToken);
                var ord = await consul.GetHealthyInstancesAsync("orders",  stoppingToken);

                var newClusters = new List<ClusterConfig>
                {
                    new ClusterConfig
                    {
                        ClusterId = "catalogCluster",
                        Destinations = cat.Select((c,i) => new { c, i })
                                          .ToDictionary(x => $"d{x.i}",
                                                        x => new DestinationConfig { Address = $"http://{x.c.Address}:{x.c.Port}/" })
                    },
                    new ClusterConfig
                    {
                        ClusterId = "ordersCluster",
                        Destinations = ord.Select((c,i) => new { c, i })
                                          .ToDictionary(x => $"d{x.i}",
                                                        x => new DestinationConfig { Address = $"http://{x.c.Address}:{x.c.Port}/" })
                    }
                };

                // Use the static routes so we don't capture top-level locals
                mem.Update(GatewayConfig.Routes, newClusters);
                logger.LogInformation("Updated YARP clusters: catalog={CatalogCount}, orders={OrdersCount}", cat.Count, ord.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update YARP from Consul");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
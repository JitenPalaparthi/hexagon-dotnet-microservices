
using Yarp.ReverseProxy;
using ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults("ApiGateway");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// YARP config (in-memory)
builder.Services.AddReverseProxy()
    .LoadFromMemory(new[]
    {
        new RouteConfig
        {
            RouteId = "catalog",
            ClusterId = "catalogCluster",
            Match = new() { Path = "/catalog/{**catch-all}" },
            Transforms = new[] { new Dictionary<string, string> { ["PathRemovePrefix"] = "/catalog" } }
        },
        new RouteConfig
        {
            RouteId = "orders",
            ClusterId = "ordersCluster",
            Match = new() { Path = "/orders/{**catch-all}" },
            Transforms = new[] { new Dictionary<string, string> { ["PathRemovePrefix"] = "/orders" } }
        }
    },
    new[]
    {
        new ClusterConfig
        {
            ClusterId = "catalogCluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new() { Address = Environment.GetEnvironmentVariable("CATALOG_BASE") ?? "http://localhost:5001/" }
            }
        },
        new ClusterConfig
        {
            ClusterId = "ordersCluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new() { Address = Environment.GetEnvironmentVariable("ORDERS_BASE") ?? "http://localhost:5003/" }
            }
        }
    });

var app = builder.Build();
app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new { ok = true, routes = new[] { "/catalog", "/orders" } }));
app.MapReverseProxy();
app.Run();

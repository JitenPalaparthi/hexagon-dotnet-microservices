
using ServiceDefaults;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ConsulOptions>(o =>
    o.Address = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? "http://localhost:8500");
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ConsulDiscoveryClient>();
builder.Services.AddHostedService<ConsulRegistrationHostedService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5003";
Environment.SetEnvironmentVariable("SERVICE_NAME", Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "orders");
Environment.SetEnvironmentVariable("SERVICE_HOST", Environment.GetEnvironmentVariable("SERVICE_HOST") ?? "localhost");
Environment.SetEnvironmentVariable("SERVICE_PORT", Environment.GetEnvironmentVariable("SERVICE_PORT") ?? port);

var app = builder.Build();

// If ASPNETCORE_URLS is not set (local run), ensure we listen on localhost:{port}
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    app.Urls.Add($"http://localhost:{port}");
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "orders" }));

app.MapPost("/v1/orders", async (ConsulDiscoveryClient consul, IHttpClientFactory hcf) =>
{
    var instances = await consul.GetHealthyInstancesAsync("catalog");
    if (instances.Count == 0)
        return Results.Problem("No healthy 'catalog' instances discovered");

    var target = instances[0];
    var client = hcf.CreateClient();
    client.BaseAddress = new Uri($"http://{target.Address}:{target.Port}/");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    var productsJson = await client.GetStringAsync("v1/products");
    var order = new { id = Guid.CreateVersion7(), createdAtUtc = DateTimeOffset.UtcNow, verifiedProductsPayload = productsJson.Length };
    return Results.Ok(order);
});



app.Run();

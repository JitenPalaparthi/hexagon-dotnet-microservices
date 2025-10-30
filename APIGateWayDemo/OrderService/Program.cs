
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using ServiceDefaults;
using System.Net.Http.Headers;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults("OrderService");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApiVersioning(o =>
{
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.ReportApiVersions = true;
    o.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer();

// Outbox (in-memory demo)
builder.Services.AddSingleton<Outbox>();

// Idempotency cache (store results by Idempotency-Key header)
builder.Services.AddSingleton(new ConcurrentDictionary<string, object>());

// Resilient HttpClient to CatalogService
builder.Services.AddHttpClient("catalog", c =>
{
    c.BaseAddress = new Uri(Environment.GetEnvironmentVariable("CATALOG_BASE") ?? "http://localhost:5001/");
    c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddStandardResilienceHandler(); // retries + timeouts + circuit breaker defaults

var app = builder.Build();
app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var v1 = app.MapGroup("/v{version:apiVersion}");

v1.MapPost("/orders", async (
    HttpContext http,
    Outbox outbox,
    IHttpClientFactory hcf) =>
{
    // Idempotency by header
    var header = "Idempotency-Key";
    var store = app.Services.GetRequiredService<ConcurrentDictionary<string, object>>();
    if (http.Request.Headers.TryGetValue(header, out var key) && !string.IsNullOrWhiteSpace(key))
    {
        if (store.TryGetValue(key!, out var existing))
            return Results.Ok(existing);
    }

    // Validate product exists in Catalog (simple call)
    var client = hcf.CreateClient("catalog");
    var resp = await client.GetAsync("v1/products");
    resp.EnsureSuccessStatusCode();
    var productsJson = await resp.Content.ReadAsStringAsync();

    var order = new Order(Guid.CreateVersion7(), DateTimeOffset.UtcNow);
    // Publish an outbox event
    outbox.Enqueue(new OrderCreated(order.Id, order.CreatedAt));

    var dto = new { order.Id, order.CreatedAt, VerifiedProductsPayload = productsJson.Length };
    if (http.Request.Headers.TryGetValue(header, out var provided) && !string.IsNullOrWhiteSpace(provided))
        store[provided!] = dto;

    return Results.Ok(dto);
})
.WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(1,0).Build()).MapToApiVersion(1,0)
.WithName("CreateOrder");

v1.MapGet("/orders/outbox", (Outbox outbox) => outbox.PeekAll())
.WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(1,0).Build()).MapToApiVersion(1,0)
.WithName("OutboxPeek");

app.Run();

record Order(Guid Id, DateTimeOffset CreatedAt);
record OrderCreated(Guid OrderId, DateTimeOffset AtUtc);

class Outbox
{
    private readonly ConcurrentQueue<OrderCreated> _events = new();

    public void Enqueue(OrderCreated e) => _events.Enqueue(e);

    public IEnumerable<OrderCreated> PeekAll() => _events.ToArray();
}


using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults("CatalogService");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// API Versioning
builder.Services.AddApiVersioning(o =>
{
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.ReportApiVersions = true;
    o.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer();

// In-memory store
builder.Services.AddSingleton<ProductStore>();

var app = builder.Build();
app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// v1 endpoints
var v1 = app.MapGroup("/v{version:apiVersion}");
v1.MapGet("/products", (ProductStore store) => store.GetAll())
  .WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(1,0).Build()).MapToApiVersion(1,0)
  .WithName("GetProducts");

v1.MapPost("/products", (ProductStore store, Product p) =>
{
    store.Add(p);
    return Results.Created($"/v1/products/{p.Id}", p);
})
  .WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(1,0).Build()).MapToApiVersion(1,0)
  .WithName("CreateProduct");

app.Run();

record Product(Guid Id, string Name, decimal Price);

class ProductStore
{
    private readonly List<Product> _items = new()
    {
        new Product(Guid.CreateVersion7(), "Keyboard", 49.99m),
        new Product(Guid.CreateVersion7(), "Mouse", 24.99m)
    };

    public IEnumerable<Product> GetAll() => _items;
    public void Add(Product p) => _items.Add(p);
}

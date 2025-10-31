using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using CrudApi.Data;
using CrudApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Connection string from env or appsettings.json
var conn = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(conn))
{
    // Fallback to single env var if provided
    conn = Environment.GetEnvironmentVariable("DB_CONNECTION") ?? "";
}

if (string.IsNullOrWhiteSpace(conn))
{
    throw new InvalidOperationException("No PostgreSQL connection string configured. Set ConnectionStrings:Default or DB_CONNECTION.");
}

// Services
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(conn));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CrudApi", Version = "v1" });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(conn);

var app = builder.Build();

// Apply database migrations (or create if not using migrations).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // For demo simplicity; in production prefer: db.Database.Migrate();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment() || Environment.GetEnvironmentVariable("ASPNETCORE_ENABLE_SWAGGER") == "true")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

// CRUD endpoints for Item
app.MapGet("/", () => Results.Redirect("/swagger", true))
   .ExcludeFromDescription();

app.MapGet("/items", async (AppDbContext db) =>
{
    var items = await db.Items.AsNoTracking().ToListAsync();
    return Results.Ok(items);
})
.WithName("GetItems");

app.MapGet("/items/{id:int}", async (int id, AppDbContext db) =>
{
    var item = await db.Items.FindAsync(id);
    return item is null ? Results.NotFound() : Results.Ok(item);
})
.WithName("GetItemById");

app.MapPost("/items", async (ItemCreateDto dto, AppDbContext db) =>
{
    var item = new Item
    {
        Name = dto.Name,
        Price = dto.Price,
        InStock = dto.InStock
    };
    db.Items.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/items/{item.Id}", item);
})
.WithName("CreateItem");

app.MapPut("/items/{id:int}", async (int id, ItemUpdateDto dto, AppDbContext db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound();

    item.Name = dto.Name;
    item.Price = dto.Price;
    item.InStock = dto.InStock;

    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("UpdateItem");

app.MapDelete("/items/{id:int}", async (int id, AppDbContext db) =>
{
    var item = await db.Items.FindAsync(id);
    if (item is null) return Results.NotFound();

    db.Items.Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("DeleteItem");

app.Run();

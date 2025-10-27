using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ConsulOptions>(o =>
    o.Address = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? "http://localhost:8500");
builder.Services.AddHttpClient();
builder.Services.AddHostedService<ConsulRegistrationHostedService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
Environment.SetEnvironmentVariable("SERVICE_NAME", Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "catalog");
Environment.SetEnvironmentVariable("SERVICE_HOST", Environment.GetEnvironmentVariable("SERVICE_HOST") ?? "catalog");
Environment.SetEnvironmentVariable("SERVICE_PORT", Environment.GetEnvironmentVariable("SERVICE_PORT") ?? port);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "catalog" }));

var products = new[] {
    new { id = Guid.CreateVersion7(), name = "Keyboard", price = 49.99 },
    new { id = Guid.CreateVersion7(), name = "Mouse",    price = 24.99 }
};
app.MapGet("/v1/products", () => Results.Ok(products));

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    app.Urls.Add($"http://localhost:{port}");
}

app.Run();

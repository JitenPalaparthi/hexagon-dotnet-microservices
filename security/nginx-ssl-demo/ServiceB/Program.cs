var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => new {
    service = "ServiceB",
    time = DateTimeOffset.UtcNow,
    message = "Hello from Service B!"
});

app.MapGet("/whoami", () => "Service B (minimal API)");
app.Run();

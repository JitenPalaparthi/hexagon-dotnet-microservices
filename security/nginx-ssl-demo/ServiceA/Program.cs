var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => new {
    service = "ServiceA",
    time = DateTimeOffset.UtcNow,
    message = "Hello from Service A!"
});

app.MapGet("/whoami", () => "Service A (minimal API)");
app.Run();

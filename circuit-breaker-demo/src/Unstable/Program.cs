using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var rand = new Random();

double failureRate = double.TryParse(Environment.GetEnvironmentVariable("FAILURE_RATE"), out var fr) ? Math.Clamp(fr, 0, 1) : 0.5;
int minDelay = int.TryParse(Environment.GetEnvironmentVariable("MIN_MS_DELAY"), out var md) ? md : 50;
int maxDelay = int.TryParse(Environment.GetEnvironmentVariable("MAX_MS_DELAY"), out var xd) ? xd : 500;
bool hardFail = bool.TryParse(Environment.GetEnvironmentVariable("HARD_FAIL"), out var hf) && hf;

app.MapGet("/", () => new
{
    message = "Unstable service. Hit /api/unstable",
    env = new { failureRate, minDelay, maxDelay, hardFail }
});

app.MapGet("/api/unstable", async () =>
{
    // random variable work
    var d = rand.Next(minDelay, maxDelay + 1);
    await Task.Delay(d);

    if (hardFail || rand.NextDouble() < failureRate)
    {
        return Results.StatusCode(500);
    }

    return Results.Ok(new
    {
        ok = true,
        delayMs = d,
        time = DateTimeOffset.UtcNow
    });
});

app.Run();

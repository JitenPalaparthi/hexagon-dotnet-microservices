using System.Net;
using System.Text.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

var builder = WebApplication.CreateBuilder(args);

// HttpClient pointing to the downstream unstable service
builder.Services.AddHttpClient("unstable", client =>
{
    var baseUrl = builder.Configuration["Downstream__BaseUrl"] ?? "http://unstable:8081";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

// --- Circuit Breaker + Retry using Polly v8 ---

// shared state we will expose via /cb/state
var state = "closed";
var lastStateChangeUtc = DateTimeOffset.UtcNow;

var breaker = new CircuitBreakerStrategyOptions<HttpResponseMessage>
{
    FailureRatio = 0.5,                         // 50% failures in the sampling window will trip
    MinimumThroughput = 8,                      // need at least 8 calls in the window
    SamplingDuration = TimeSpan.FromSeconds(30),
    BreakDuration = TimeSpan.FromSeconds(10),
    ShouldHandle = args =>
        ValueTask.FromResult(
            args.Outcome switch
            {
                { Exception: TimeoutException } => true,
                { Exception: HttpRequestException } => true,
                _ => args.Outcome.Result is { } r && ((int)r.StatusCode >= 500)
            }),
    OnOpened = args =>
    {
        state = "open";
        lastStateChangeUtc = DateTimeOffset.UtcNow;
        app.Logger.LogWarning("Circuit opened for {BreakDuration}", args.BreakDuration);
        return default;
    },
    OnClosed = _ =>
    {
        state = "closed";
        lastStateChangeUtc = DateTimeOffset.UtcNow;
        app.Logger.LogInformation("Circuit closed");
        return default;
    },
    OnHalfOpened = _ =>
    {
        state = "half-open";
        lastStateChangeUtc = DateTimeOffset.UtcNow;
        app.Logger.LogInformation("Circuit half-open - allowing trial calls");
        return default;
    }
};

var retry = new RetryStrategyOptions<HttpResponseMessage>
{
    MaxRetryAttempts = 3,
    BackoffType = DelayBackoffType.Exponential,
    Delay = TimeSpan.FromMilliseconds(200),
    UseJitter = true,
    ShouldHandle = args =>
        ValueTask.FromResult(
            args.Outcome switch
            {
                { Exception: TimeoutException } => true,
                { Exception: HttpRequestException } => true,
                _ => args.Outcome.Result is { } r && ((int)r.StatusCode >= 500)
            })
};

var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(retry)
    .AddCircuitBreaker(breaker)
    .Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "Gateway with Circuit Breaker demo. Try /call and /cb/state. Env: DOWNSTREAM__BASEURL to override downstream URL."
}));

app.MapGet("/cb/state", () => Results.Ok(new
{
    state,
    lastStateChangeUtc
}));

app.MapGet("/call", async (IHttpClientFactory httpClientFactory, CancellationToken ct) =>
{
    var client = httpClientFactory.CreateClient("unstable");
    var response = await pipeline.ExecuteAsync(async token =>
    {
        return await client.GetAsync("/api/unstable", token);
    }, ct);

    var body = await response.Content.ReadAsStringAsync(ct);
    return Results.Json(new
    {
        statusCode = (int)response.StatusCode,
        downstream = JsonSerializer.Deserialize<object>(body),
        breakerState = state
    });
});

app.Run();

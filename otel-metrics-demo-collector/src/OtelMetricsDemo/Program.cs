using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

var otlpEndpoint = builder.Configuration["OTLP__Endpoint"] ?? "http://otel-collector:4317";

builder.Services.AddOpenTelemetry()
    .WithMetrics(mb =>
    {
        mb.AddAspNetCoreInstrumentation();
        mb.AddRuntimeInstrumentation();
        mb.AddMeter("Demo.Metrics");
        mb.AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otlpEndpoint);
            o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        });
    });

var app = builder.Build();

var meter = new Meter("Demo.Metrics", "1.0.0");
var requestCounter = meter.CreateCounter<long>("demo_requests_total");
var qDepth = 0;
var queueGauge = meter.CreateObservableGauge("demo_queue_depth", () => new Measurement<int>(qDepth));
var durationHist = meter.CreateHistogram<double>("demo_processing_duration_seconds", unit: "s");

var queue = new Queue<string>();

app.MapGet("/", () => Results.Ok(new
{
    message = "OTLP -> OTel Collector -> Prometheus demo",
    tryThese = new[] { "/enqueue?item=x", "/dequeue", "/work?ms=250" }
}));

app.MapPost("/enqueue", (string item) =>
{
    queue.Enqueue(item);
    qDepth = queue.Count;
    requestCounter.Add(1, KeyValuePair.Create<string, object?>("endpoint", "enqueue"));
    return Results.Accepted($"/queue/{queue.Count}", new { enqueued = item, depth = qDepth });
});

app.MapPost("/dequeue", () =>
{
    if (queue.Count == 0)
    {
        requestCounter.Add(1, KeyValuePair.Create<string, object?>("endpoint", "dequeue_empty"));
        return Results.NoContent();
    }
    var item = queue.Dequeue();
    qDepth = queue.Count;
    requestCounter.Add(1, KeyValuePair.Create<string, object?>("endpoint", "dequeue"));
    return Results.Ok(new { dequeued = item, depth = qDepth });
});

app.MapGet("/work", async (int ms) =>
{
    var sw = Stopwatch.StartNew();
    await Task.Delay(ms);
    sw.Stop();
    durationHist.Record(sw.Elapsed.TotalSeconds, KeyValuePair.Create<string, object?>("work_ms", ms));
    requestCounter.Add(1, KeyValuePair.Create<string, object?>("endpoint", "work"));
    return Results.Ok(new { simulated_ms = ms, elapsed_ms = sw.ElapsedMilliseconds });
});

app.Run();

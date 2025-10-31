using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

var otlpEndpoint = builder.Configuration["OTLP__Endpoint"] ?? "http://otel-collector:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(rb => rb.AddService(serviceName: "otel-metrics-demo-app", serviceVersion: "1.0.0"))
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

// Custom metrics
var meter = new Meter("Demo.Metrics", "1.0.0");
var requestCounter = meter.CreateCounter<long>("demo_requests_total");
var qDepth = 0;
var queueGauge = meter.CreateObservableGauge("demo_queue_depth", () => new Measurement<int>(qDepth));
var durationHist = meter.CreateHistogram<double>("demo_processing_duration_seconds", unit: "s");
var rnd = new Random();

// Background generator: emits traffic every second so you ALWAYS see metrics
var cts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        var ms = rnd.Next(50, 750);
        var sw = Stopwatch.StartNew();
        await Task.Delay(ms, cts.Token);
        sw.Stop();
        durationHist.Record(sw.Elapsed.TotalSeconds, KeyValuePair.Create<string, object?>("work_ms", ms));
        requestCounter.Add(1, KeyValuePair.Create<string, object?>("endpoint", "bg_work"));
        // mutate queue depth a bit
        qDepth = Math.Clamp(qDepth + (rnd.NextDouble() < 0.5 ? 1 : -1), 0, 20);
        await Task.Delay(1000, cts.Token);
    }
}, cts.Token);

app.Lifetime.ApplicationStopping.Register(() => cts.Cancel());

app.MapGet("/", () => Results.Ok(new
{
    message = "OTLP -> OTel Collector -> Prometheus (diagnostics build)",
    info = "Background task periodically emits metrics so you should see data without any manual calls."
}));

app.Run();

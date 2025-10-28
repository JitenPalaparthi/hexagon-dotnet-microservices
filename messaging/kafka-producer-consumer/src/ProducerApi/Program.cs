using Confluent.Kafka;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration.GetSection("Kafka");
var bootstrap = cfg["BootstrapServers"] ?? Environment.GetEnvironmentVariable("Kafka__BootstrapServers") ?? "kafka:9092";
var topic = cfg["Topic"] ?? Environment.GetEnvironmentVariable("Kafka__Topic") ?? "demo-messages";

builder.Services.AddSingleton(new ProducerConfig
{
    BootstrapServers = bootstrap,
    Acks = Acks.All,
    EnableIdempotence = true,
    MessageTimeoutMs = 30000
});

builder.Services.AddSingleton<IProducer<Null, string>>(sp =>
{
    var pc = sp.GetRequiredService<ProducerConfig>();
    return new ProducerBuilder<Null, string>(pc).Build();
});

var app = WebApplication.Create();

app.MapGet("/", () => Results.Ok(new
{
    service = "ProducerApi",
    produce = "POST /produce with { text?: string }",
    topic
}));

app.MapPost("/produce", async (IProducer<Null, string> producer, dynamic body) =>
{
    string text = (string?)body?.text ?? (string?)body?.Text ?? "hello from .NET 9";
    var payload = new
    {
        id = Guid.NewGuid().ToString("n"),
        sentAt = DateTimeOffset.UtcNow,
        text
    };

    var json = JsonSerializer.Serialize(payload);
    var dr = await producer.ProduceAsync(topic, new Message<Null, string> { Value = json });

    return Results.Ok(new
    {
        status = "enqueued",
        topic,
        partition = dr.Partition.Value,
        offset = dr.Offset.Value,
        payload
    });
});

app.Run();

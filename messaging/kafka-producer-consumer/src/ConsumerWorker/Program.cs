using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var cfg = builder.Configuration.GetSection("Kafka");
var bootstrap = cfg["BootstrapServers"] ?? Environment.GetEnvironmentVariable("Kafka__BootstrapServers") ?? "kafka:9092";
var topic = cfg["Topic"] ?? Environment.GetEnvironmentVariable("Kafka__Topic") ?? "demo-messages";
var groupId = cfg["GroupId"] ?? Environment.GetEnvironmentVariable("Kafka__GroupId") ?? "dotnet-consumer-1";
var aor = cfg["AutoOffsetReset"] ?? Environment.GetEnvironmentVariable("Kafka__AutoOffsetReset") ?? "Earliest";

builder.Services.AddHostedService(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Consumer");
    var conf = new ConsumerConfig
    {
        BootstrapServers = bootstrap,
        GroupId = groupId,
        AutoOffsetReset = Enum.TryParse<AutoOffsetReset>(aor, true, out var parsed) ? parsed : AutoOffsetReset.Earliest,
        EnableAutoCommit = true
    };

    return new BackgroundServiceImpl(logger, conf, topic);
});

var app = builder.Build();
await app.RunAsync();

sealed class BackgroundServiceImpl(ILogger logger, ConsumerConfig config, string topic) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => RunLoop(stoppingToken), stoppingToken);

    private void RunLoop(CancellationToken ct)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(topic);

        logger.LogInformation("Consumer started. Topic: {Topic}, GroupId: {Group}", topic, config.GroupId);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(ct);
                    logger.LogInformation("Consumed: partition={Partition} offset={Offset} value={Value}",
                        cr.Partition.Value, cr.Offset.Value, cr.Message.Value);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Consume error: {Reason}", ex.Error.Reason);
                }
            }
        }
        catch (OperationCanceledException) { /* normal on shutdown */ }
        finally
        {
            try { consumer.Close(); } catch { /* ignore */ }
            logger.LogInformation("Consumer stopped.");
        }
    }
}

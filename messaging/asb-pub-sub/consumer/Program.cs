// Consumer/Program.cs (net9)
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var conn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING")
          ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
var topicName = Environment.GetEnvironmentVariable("TOPIC__NAME");              // e.g., demo-topic
var subscriptionName = Environment.GetEnvironmentVariable("SUBSCRIPTION__NAME"); // e.g., sub-a
var queueName = Environment.GetEnvironmentVariable("QUEUE__NAME") ?? "demo-queue";

var maxConcurrent = int.TryParse(Environment.GetEnvironmentVariable("PROCESSOR__MAXCONCURRENT"), out var c) ? c : 4;
var prefetch = int.TryParse(Environment.GetEnvironmentVariable("PROCESSOR__PREFETCH"), out var p) ? p : 10;

builder.Services.AddSingleton(new ServiceBusClient(conn));
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();

class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly ServiceBusClient _client;

    public Worker(ILogger<Worker> log, ServiceBusClient client)
    {
        _log = log; _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topic = Environment.GetEnvironmentVariable("TOPIC__NAME");
        var sub = Environment.GetEnvironmentVariable("SUBSCRIPTION__NAME");
        var queue = Environment.GetEnvironmentVariable("QUEUE__NAME") ?? "demo-queue";

        var maxConcurrent = int.TryParse(Environment.GetEnvironmentVariable("PROCESSOR__MAXCONCURRENT"), out var c) ? c : 4;
        var prefetch = int.TryParse(Environment.GetEnvironmentVariable("PROCESSOR__PREFETCH"), out var p) ? p : 10;

        var opts = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = Math.Max(1, maxConcurrent),
            PrefetchCount = Math.Max(0, prefetch),
            AutoCompleteMessages = false
        };

        ServiceBusProcessor processor =
            (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(sub))
                ? _client.CreateProcessor(topic, sub, opts)   // Topic/Subscription
                : _client.CreateProcessor(queue, opts);        // Queue fallback

        processor.ProcessMessageAsync += async args =>
        {
            var body = args.Message.Body.ToString();
            var mtype = args.Message.ApplicationProperties.TryGetValue("message-type", out var v) ? v?.ToString() : "unknown";
            _log.LogInformation("SUB={Sub} MSG={Id} TYPE={Type} BODY={Body}", sub ?? "-", args.Message.MessageId, mtype, body);
            await Task.Delay(50, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message);
        };

        processor.ProcessErrorAsync += args =>
        {
            _log.LogError(args.Exception, "ASB error Source={Source} Entity={Entity}", args.ErrorSource, args.EntityPath);
            return Task.CompletedTask;
        };

        _log.LogInformation("Starting processor: {Mode}", (!string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(sub))
            ? $"topic={topic} subscription={sub}"
            : $"queue={queue}");

        await processor.StartProcessingAsync(stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await processor.StopProcessingAsync();
            await processor.DisposeAsync();
        }
    }
}
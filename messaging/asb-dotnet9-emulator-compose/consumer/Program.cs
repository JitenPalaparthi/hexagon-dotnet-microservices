using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Explicit environment read + config
var envConn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING");
builder.Configuration.AddEnvironmentVariables();
var cfgConn = builder.Configuration["SERVICEBUS__CONNECTIONSTRING"];

var connectionString = !string.IsNullOrWhiteSpace(envConn)
    ? envConn
    : (!string.IsNullOrWhiteSpace(cfgConn) ? cfgConn : null);

// Optional fallback to emulator (remove if undesired)
connectionString ??= "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("SERVICEBUS__CONNECTIONSTRING not set");

var queueName = builder.Configuration["QUEUE__NAME"] ?? "demo-queue";

var maxConcurrent = int.TryParse(builder.Configuration["PROCESSOR__MAXCONCURRENT"], out var c) ? c : 4;
var prefetch = int.TryParse(builder.Configuration["PROCESSOR__PREFETCH"], out var p) ? p : 10;

builder.Services.AddSingleton(new ServiceBusClient(connectionString));
builder.Services.AddHostedService<Worker>();


var host = builder.Build();
await host.RunAsync();

class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient _client;
    private readonly IConfiguration _cfg;
    private ServiceBusProcessor? _processor;

    public Worker(ILogger<Worker> logger, ServiceBusClient client, IConfiguration cfg)
    {
        _logger = logger;
        _client = client;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = _cfg["QUEUE__NAME"] ?? "demo-queue";
        var maxConcurrent = int.TryParse(_cfg["PROCESSOR__MAXCONCURRENT"], out var c) ? c : 4;
        var prefetch = int.TryParse(_cfg["PROCESSOR__PREFETCH"], out var p) ? p : 10;

        var options = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = Math.Max(1, maxConcurrent),
            PrefetchCount = Math.Max(0, prefetch),
            AutoCompleteMessages = false,
        };

        _processor = _client.CreateProcessor(queueName, options);
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        _logger.LogInformation("Starting processor for queue '{Queue}' (maxConcurrent={Max}, prefetch={Prefetch})",
            queueName, options.MaxConcurrentCalls, options.PrefetchCount);

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(500, stoppingToken);
        }
        finally
        {
            await _processor.StopProcessingAsync();
            await _processor.DisposeAsync();
        }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var body = args.Message.Body.ToString();
            var contentType = args.Message.ContentType;
            var messageType = args.Message.ApplicationProperties.TryGetValue("message-type", out var val)
                ? val?.ToString()
                : "unknown";

            _logger.LogInformation("Received messageId={Id}, type={Type}, contentType={CT}, body={Body}",
                args.Message.MessageId, messageType, contentType, body);

            await Task.Delay(100);
            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            await args.DeadLetterMessageAsync(args.Message, deadLetterReason: "processing_failed", deadLetterErrorDescription: ex.Message);
            _logger.LogError(ex, "Failed to process messageId={Id}", args.Message.MessageId);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        Console.WriteLine($"[ASB ERROR] Entity={args.EntityPath} Source={args.ErrorSource} Exception={args.Exception.Message}");
        return Task.CompletedTask;
    }
}

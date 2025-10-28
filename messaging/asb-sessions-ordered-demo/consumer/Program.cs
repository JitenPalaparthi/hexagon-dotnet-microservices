using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var conn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING")
          ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
var queue = Environment.GetEnvironmentVariable("QUEUE__NAME") ?? "orders-queue";

var maxSessions = int.TryParse(Environment.GetEnvironmentVariable("MAX_CONCURRENT_SESSIONS"), out var s) ? s : 4;
var prefetch = int.TryParse(Environment.GetEnvironmentVariable("PREFETCH"), out var p) ? p : 50;

// Session processor enforces FIFO per SessionId
var client = new ServiceBusClient(conn);
var options = new ServiceBusSessionProcessorOptions
{
    MaxConcurrentSessions = Math.Max(1, maxSessions),
    MaxConcurrentCallsPerSession = 1,     // preserve order
    PrefetchCount = Math.Max(0, prefetch),
    AutoCompleteMessages = false,
    SessionIdleTimeout = TimeSpan.FromSeconds(30)
};

var processor = client.CreateSessionProcessor(queue, options);

processor.ProcessMessageAsync += async args =>
{
    var sid = args.SessionId;
    var seq = args.Message.SequenceNumber;
    var body = args.Message.Body.ToString();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SID={sid} SEQ={seq} BODY={body}");

    // simulate work
    await Task.Delay(50, args.CancellationToken);
    await args.CompleteMessageAsync(args.Message);
};

processor.ProcessErrorAsync += async args =>
{
    Console.WriteLine($"[ASB ERROR] Source={args.ErrorSource} Entity={args.EntityPath} Ex={args.Exception.Message}");
    await Task.CompletedTask;
};

Console.WriteLine($"Starting session processor on queue '{queue}' (sessions={options.MaxConcurrentSessions}, prefetch={options.PrefetchCount})");
await processor.StartProcessingAsync();

Console.WriteLine("Press ENTER to stop...");
Console.ReadLine();

await processor.StopProcessingAsync();
await processor.DisposeAsync();

using Azure.Messaging.ServiceBus;

var conn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING")
          ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
var queue = Environment.GetEnvironmentVariable("QUEUE__NAME") ?? "orders-queue";

var maxSessions = int.TryParse(Environment.GetEnvironmentVariable("MAX_CONCURRENT_SESSIONS"), out var s) ? s : 4;
var prefetch = int.TryParse(Environment.GetEnvironmentVariable("PREFETCH"), out var p) ? p : 50;
var failStepEnv = Environment.GetEnvironmentVariable("SIM_FAIL_STEP");
int? failStep = int.TryParse(failStepEnv, out var fs) ? fs : null;

var client = new ServiceBusClient(conn);
var options = new ServiceBusSessionProcessorOptions
{
    MaxConcurrentSessions = Math.Max(1, maxSessions),
    MaxConcurrentCallsPerSession = 1,
    PrefetchCount = Math.Max(0, prefetch),
    AutoCompleteMessages = false,
    SessionIdleTimeout = TimeSpan.FromSeconds(30)
};
var processor = client.CreateSessionProcessor(queue, options);

processor.ProcessMessageAsync += async args =>
{
    var sid = args.SessionId;
    var body = args.Message.Body.ToString();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SID={sid} SEQ={args.Message.SequenceNumber} BODY={body}");

    if (failStep.HasValue && body.Contains($"\"step\":{failStep.Value}"))
    {
        Console.WriteLine($"❌ Simulated failure for step {failStep.Value} -> will retry then DLQ");
        throw new Exception("Simulated failure for DLQ demo");
    }

    await Task.Delay(50, args.CancellationToken);
    await args.CompleteMessageAsync(args.Message);
};

processor.ProcessErrorAsync += args =>
{
    Console.WriteLine($"[ASB ERROR] Source={args.ErrorSource} Entity={args.EntityPath} Ex={args.Exception.Message}");
    return Task.CompletedTask;
};

Console.WriteLine($"Starting session processor on '{queue}' (sessions={options.MaxConcurrentSessions}, prefetch={options.PrefetchCount}, failStep={failStep?.ToString() ?? "none"})");
await processor.StartProcessingAsync();

Console.WriteLine("Press ENTER to stop...");
Console.ReadLine();

await processor.StopProcessingAsync();
await processor.DisposeAsync();

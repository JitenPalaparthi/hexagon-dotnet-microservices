using Azure.Messaging.ServiceBus;

var conn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING")
          ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
var queue = Environment.GetEnvironmentVariable("QUEUE__NAME") ?? "orders-queue";
var requeue = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DLQ_REQUEUE"));

var dlqPath = $"{queue}/$DeadLetterQueue";
var client = new ServiceBusClient(conn);
var receiver = client.CreateReceiver(dlqPath);
var sender = client.CreateSender(queue);

Console.WriteLine($"Reading DLQ: {dlqPath}  (requeue={requeue})");

while (true)
{
    var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(3));
    if (msg == null)
    {
        Console.WriteLine("No more DLQ messages.");
        break;
    }

    var body = msg.Body.ToString();
    Console.WriteLine($"💀 DLQ MessageId={msg.MessageId}  SessionId={msg.SessionId}");
    Console.WriteLine($"Reason={msg.DeadLetterReason}  Description={msg.DeadLetterErrorDescription}");
    Console.WriteLine($"Body={body}");

    if (requeue)
    {
        var forward = new ServiceBusMessage(msg.Body)
        {
            SessionId = msg.SessionId,
            ContentType = msg.ContentType
        };
        foreach (var kvp in msg.ApplicationProperties)
            forward.ApplicationProperties[kvp.Key] = kvp.Value;
        await sender.SendMessageAsync(forward);
        Console.WriteLine($"↩️  Requeued to '{queue}'");
    }

    await receiver.CompleteMessageAsync(msg);
}

await receiver.DisposeAsync();
await sender.DisposeAsync();
await client.DisposeAsync();

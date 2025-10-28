// Producer/Program.cs (net9)
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

// Explicit env read (works reliably)
var conn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING")
          ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
var topicName = Environment.GetEnvironmentVariable("TOPIC__NAME");        // e.g., demo-topic
var queueName = Environment.GetEnvironmentVariable("QUEUE__NAME") ?? "demo-queue";

var client = new ServiceBusClient(conn);
builder.Services.AddSingleton(client);

var app = builder.Build();

app.MapPost("/send", async (HttpContext http, int? count) =>
{
    using var reader = new StreamReader(http.Request.Body);
    var body = await reader.ReadToEndAsync();
    var n = Math.Max(1, count ?? 1);

    // Choose topic or queue
    var sender = string.IsNullOrWhiteSpace(topicName)
        ? client.CreateSender(queueName)
        : client.CreateSender(topicName);

    var batch = await sender.CreateMessageBatchAsync();
    for (int i = 0; i < n; i++)
    {
        var msg = new ServiceBusMessage(body) { ContentType = "application/json" };
        msg.ApplicationProperties["message-type"] = "demo";
        if (!batch.TryAddMessage(msg))
        {
            await sender.SendMessagesAsync(batch);
            batch = await sender.CreateMessageBatchAsync();
            if (!batch.TryAddMessage(msg)) return Results.Problem($"Message too large at {i}");
        }
    }

    await sender.SendMessagesAsync(batch);
    return Results.Ok(new { sent = n, mode = string.IsNullOrWhiteSpace(topicName) ? "queue" : "topic", topicName, queueName });
});

app.Run();
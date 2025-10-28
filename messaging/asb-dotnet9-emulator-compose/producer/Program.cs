using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

// Be explicit: read the environment variable first (works 100% in containers)
var envConn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING");

// Also read through the configuration pipeline (env vars, appsettings, etc.)
builder.Configuration.AddEnvironmentVariables(); // redundant in ASP.NET Core, but harmless & explicit
var cfgConn = builder.Configuration["SERVICEBUS__CONNECTIONSTRING"];

// Choose the first non-empty value
var connectionString = !string.IsNullOrWhiteSpace(envConn)
    ? envConn
    : (!string.IsNullOrWhiteSpace(cfgConn) ? cfgConn : null);

// Optional dev fallback for emulator (remove if you don’t want a fallback)
connectionString ??= "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("FATAL: SERVICEBUS__CONNECTIONSTRING not set");
    return; // or Environment.Exit(1);
}

var queueName = builder.Configuration["QUEUE__NAME"] ?? "demo-queue";

builder.Services.AddSingleton(new ServiceBusClient(connectionString));

var app = builder.Build();
app.MapGet("/healthz", () => Results.Ok(new {
    status = "ok",
    source = !string.IsNullOrWhiteSpace(envConn) ? "env" :
             (!string.IsNullOrWhiteSpace(cfgConn) ? "config" : "fallback"),
    queue = queueName
}));

// Send one or many messages
app.MapPost("/send", async (HttpContext http, ServiceBusClient sbClient, int? count) =>
{
    var sender = sbClient.CreateSender(queueName);
    using var reader = new StreamReader(http.Request.Body);
    var body = await reader.ReadToEndAsync();
    var n = Math.Max(1, count ?? 1);

    var batch = await sender.CreateMessageBatchAsync();
    for (int i = 0; i < n; i++)
    {
        var msg = new ServiceBusMessage(body) { ContentType = "application/json" };
        msg.ApplicationProperties["message-type"] = "demo";
        if (!batch.TryAddMessage(msg))
        {
            await sender.SendMessagesAsync(batch);
            batch = await sender.CreateMessageBatchAsync();
            if (!batch.TryAddMessage(msg))
                return Results.Problem($"Message too large at index {i}");
        }
    }
    await sender.SendMessagesAsync(batch);
    return Results.Ok(new { sent = n, queue = queueName });
});

app.Run();
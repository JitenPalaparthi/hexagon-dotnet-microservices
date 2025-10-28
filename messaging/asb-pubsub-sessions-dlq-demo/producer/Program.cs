using Azure.Messaging.ServiceBus;

var conn = Environment.GetEnvironmentVariable("SERVICEBUS__CONNECTIONSTRING")
          ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
var topic = Environment.GetEnvironmentVariable("TOPIC__NAME") ?? "demo-topic";

var client = new ServiceBusClient(conn);
var app = WebApplication.CreateBuilder(args).Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", topic }));

// POST /send-order?orderId=order-123&steps=3&repeat=1
app.MapPost("/send-order", async (string orderId, int steps = 3, int repeat = 1) =>
{
    if (string.IsNullOrWhiteSpace(orderId))
        return Results.BadRequest("orderId is required");
    if (steps < 1) steps = 1;
    if (repeat < 1) repeat = 1;

    var sender = client.CreateSender(topic);

    for (int r = 0; r < repeat; r++)
    {
        for (int s = 1; s <= steps; s++)
        {
            var payload = new { orderId, step = s, text = $"Step{s}" };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var msg = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                SessionId = orderId,
                MessageId = $"{orderId}-{r}-{s}-{Guid.NewGuid()}"
            };
            msg.ApplicationProperties["message-type"] = "order-event";
            await sender.SendMessageAsync(msg);
        }
    }

    return Results.Ok(new { orderId, steps, repeat, topic });
});

app.Run("http://0.0.0.0:8080");

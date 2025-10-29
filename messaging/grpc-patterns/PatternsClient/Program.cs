using Grpc.Core;
using Grpc.Net.Client;
using Patterns.Grpc;
using PatternsGrpc = Patterns.Grpc;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var address = args.Length > 0 ? args[0] : "http://localhost:50000";
using var channel = GrpcChannel.ForAddress(address);
var client = new PatternsGrpc.Patterns.PatternsClient(channel);

Console.WriteLine($"Connecting to {address} ...");

await DemoUnary(client);
await DemoServerStreaming(client);
await DemoClientStreaming(client);
await DemoBidiStreaming(client);

Console.WriteLine("All demos done.");

static async Task DemoUnary(PatternsGrpc.Patterns.PatternsClient client)
{
    Console.WriteLine("\n=== Unary (request-reply) ===");
    var resp = await client.UnaryEchoAsync(new EchoRequest { Text = "hello" });
    Console.WriteLine($"Unary response: {resp.Text}");
}

static async Task DemoServerStreaming(PatternsGrpc.Patterns.PatternsClient client)
{
    Console.WriteLine("\n=== Server Streaming (half duplex server->client) ===");
    using var call = client.ServerStreamNumbers(new NumbersRequest { Count = 5 });
    while (await call.ResponseStream.MoveNext(CancellationToken.None))
    {
        Console.WriteLine($"Number: {call.ResponseStream.Current.Value}");
    }
}

static async Task DemoClientStreaming(PatternsGrpc.Patterns.PatternsClient client)
{
    Console.WriteLine("\n=== Client Streaming (half duplex client->server) ===");
    using var call = client.ClientStreamSum();

    for (int i = 1; i <= 5; i++)
        await call.RequestStream.WriteAsync(new SumRequest { Value = i });

    await call.RequestStream.CompleteAsync();
    var reply = await call;
    Console.WriteLine($"Sum reply: {reply.Sum}");
}

static async Task DemoBidiStreaming(PatternsGrpc.Patterns.PatternsClient client)
{
    Console.WriteLine("\n=== Full Duplex (bidirectional streaming) ===");
    using var call = client.Chat();

    var reader = Task.Run(async () =>
    {
        try
        {
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                var m = call.ResponseStream.Current;
                Console.WriteLine($"Server says: {m.Text} (ts={m.UnixTimeMs})");
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
    });

    await call.RequestStream.WriteAsync(new ChatMessage { From = "client", Text = "Hi", UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    await Task.Delay(200);
    await call.RequestStream.WriteAsync(new ChatMessage { From = "client", Text = "How are you?", UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    await Task.Delay(200);
    await call.RequestStream.WriteAsync(new ChatMessage { From = "client", Text = "Bye", UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

    await call.RequestStream.CompleteAsync();
    await reader;
}

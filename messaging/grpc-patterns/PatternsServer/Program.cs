using Grpc.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Patterns.Grpc;

var builder = WebApplication.CreateBuilder(args);
var portEnv = Environment.GetEnvironmentVariable("PORT");
var listenPort = string.IsNullOrWhiteSpace(portEnv) ? 50000 : int.Parse(portEnv);

builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(listenPort, lo => lo.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<PatternsService>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();
app.MapGet("/", () => $"gRPC Patterns server on http://localhost:{listenPort} (h2c)");

app.Run();

public sealed class PatternsService : global::Patterns.Grpc.Patterns.PatternsBase
{
    public override Task<EchoReply> UnaryEcho(EchoRequest request, ServerCallContext context)
        => Task.FromResult(new EchoReply { Text = $"echo: {request.Text}" });

    public override async Task ServerStreamNumbers(NumbersRequest request, IServerStreamWriter<Number> responseStream, ServerCallContext context)
    {
        var count = Math.Max(1, request.Count);
        for (int i = 1; i <= count; i++)
        {
            await responseStream.WriteAsync(new Number { Value = i });
            await Task.Delay(200, context.CancellationToken);
        }
    }

    public override async Task<SumReply> ClientStreamSum(IAsyncStreamReader<SumRequest> requestStream, ServerCallContext context)
    {
        long sum = 0;
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            sum += requestStream.Current.Value;
        }
        return new SumReply { Sum = sum };
    }

    public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
    {
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            var m = requestStream.Current;
            await responseStream.WriteAsync(new ChatMessage
            {
                From = "server",
                Text = $"[{m.From}] {m.Text}",
                UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }
}

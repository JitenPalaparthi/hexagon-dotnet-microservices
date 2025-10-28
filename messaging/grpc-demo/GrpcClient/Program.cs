using System;
using System.Threading.Tasks;
using System.Threading;
using Grpc.Net.Client;
using Demo.Grpc;
using Grpc.Core;

class Program
{
    static async Task Main(string[] args)
    {
        // Enable HTTP/2 without TLS for localhost dev (h2c)
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var target = args.Length > 0 ? args[0] : "http://localhost:5000";
        var name = args.Length > 1 ? args[1] : "Jiten";

        Console.WriteLine($"Connecting to {target} ...");

        using var channel = GrpcChannel.ForAddress(target); // listen and serve
        var client = new Greeter.GreeterClient(channel);

        // Unary
        var reply = await client.SayHelloAsync(new HelloRequest { Name = name });
        Console.WriteLine($"Unary: {reply.Message}");

        // Server streaming (compatible loop without ReadAllAsync)
        Console.WriteLine("Server streaming:");
        using var call = client.GreetManyTimes(new HelloRequest { Name = name });
        while (await call.ResponseStream.MoveNext(CancellationToken.None))
        {
            var msg = call.ResponseStream.Current;
            Console.WriteLine($"  {msg.Message}");
        }

        Console.WriteLine("Done.");
    }
}

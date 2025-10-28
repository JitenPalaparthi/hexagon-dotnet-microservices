using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;

public record MyAppOptions
{
    public string ApiKey { get; init; } = "";
    public string DbPassword { get; init; } = "";
}

class Program
{
    static async Task<IDictionary<string,string>> ReadVaultAsync(IConfiguration cfg)
    {
        var addr = cfg["VAULT_ADDR"] ?? "http://127.0.0.1:8200";
        var token = cfg["VAULT_TOKEN"] ?? "root";
        var mount = cfg["VAULT_MOUNT"] ?? "secret"; // kv v2 mount name
        var pathsCsv = cfg["VAULT_PATHS"] ?? "app"; // comma separated logical paths

        IAuthMethodInfo auth = new TokenAuthMethodInfo(token);
        var settings = new VaultClientSettings(addr, auth)
        {
            ContinueAsyncTasksOnCapturedContext = false
        };
        var client = new VaultClient(settings);

        var dict = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in pathsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var secret = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path, mountPoint: mount);
            foreach (var kv in secret.Data.Data)
            {
                Flatten(dict, kv.Key, kv.Value);
            }
        }

        return dict;

        static void Flatten(Dictionary<string,string> acc, string prefix, object? value)
        {
            switch (value)
            {
                case null:
                    return;
                case string s:
                    acc[prefix] = s;
                    break;
                case bool b:
                    acc[prefix] = b.ToString();
                    break;
                case int i:
                    acc[prefix] = i.ToString();
                    break;
                case long l:
                    acc[prefix] = l.ToString();
                    break;
                case double d:
                    acc[prefix] = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case IDictionary<string, object> map:
                    foreach (var m in map)
                        Flatten(acc, $"{prefix}:{m.Key}", m.Value);
                    break;
                case IEnumerable<KeyValuePair<string, object>> map2:
                    foreach (var m in map2)
                        Flatten(acc, $"{prefix}:{m.Key}", m.Value);
                    break;
                default:
                    // fallback to JSON string
                    acc[prefix] = System.Text.Json.JsonSerializer.Serialize(value);
                    break;
            }
        }
    }

    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Load env and appsettings.json first
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables();

        // Pull from Vault and merge into configuration as highest precedence
        var vaultKVs = await ReadVaultAsync(builder.Configuration);
        builder.Configuration.AddInMemoryCollection(vaultKVs.Select(kv => new KeyValuePair<string,string?>(kv.Key, kv.Value)));

        // Bind options
        builder.Services.AddOptions<MyAppOptions>()
            .Bind(builder.Configuration.GetSection("MyApp"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "MyApp:ApiKey required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DbPassword), "MyApp:DbPassword required");

        builder.Services.AddHostedService<App>();

        await builder.Build().RunAsync();
    }

    sealed class App : IHostedService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IOptions<MyAppOptions> _opts;
        private readonly IConfiguration _cfg;

        public App(IHostApplicationLifetime life, IOptions<MyAppOptions> opts, IConfiguration cfg)
        {
            _lifetime = life;
            _opts = opts;
            _cfg = cfg;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("=== Effective Configuration Snapshot (selected keys) ===");
            Console.WriteLine($"VAULT_ADDR = {_cfg["VAULT_ADDR"]}");
            Console.WriteLine($"VAULT_MOUNT = {_cfg["VAULT_MOUNT"]}");
            Console.WriteLine($"VAULT_PATHS = {_cfg["VAULT_PATHS"]}");

            Console.WriteLine();
            Console.WriteLine("=== Values resolved via Vault and bound to IOptions ===");
            Console.WriteLine($"MyApp:ApiKey = {_cfg["MyApp:ApiKey"]}");
            Console.WriteLine($"MyApp:DbPassword = {_cfg["MyApp:DbPassword"]}");

            _lifetime.StopApplication();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

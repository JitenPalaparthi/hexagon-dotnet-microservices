using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServiceDefaults;

public sealed class ConsulOptions
{
    public string Address { get; set; } = "http://localhost:8500";
}

public sealed class ConsulServiceRegistration
{
    public string ID { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Address { get; set; } = default!;
    public int Port { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public ConsulCheck Check { get; set; } = new();
}

public sealed class ConsulCheck
{
    public string HTTP { get; set; } = default!;
    public string Method { get; set; } = "GET";
    public string Interval { get; set; } = "10s";
    public string DeregisterCriticalServiceAfter { get; set; } = "1m";
}

public sealed class ConsulRegistrationHostedService(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceDefaults.ConsulOptions> consulOpts,   // ← fully qualified
    IHostApplicationLifetime lifetime,
    ILogger<ConsulRegistrationHostedService> logger) : IHostedService
{
    private string? _serviceId;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() => _ = RegisterAsync(cancellationToken));
        lifetime.ApplicationStopping.Register(() => _ = DeregisterAsync(CancellationToken.None));
        return Task.CompletedTask;
    }

    public async Task RegisterAsync(CancellationToken ct)
    {
        try
        {
            var consul = consulOpts.Value.Address.TrimEnd('/');
            var client = httpClientFactory.CreateClient("consul");
            client.BaseAddress = new Uri(consul);

            var appName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "unknown";
            var appAddr = Environment.GetEnvironmentVariable("SERVICE_HOST") ?? "localhost";
            var portStr = Environment.GetEnvironmentVariable("SERVICE_PORT") ?? "0";
            var port = int.TryParse(portStr, out var p) ? p : 0;

            _serviceId = $"{appName}-{Guid.NewGuid():N}";

            var registration = new ConsulServiceRegistration
            {
                ID = _serviceId,
                Name = appName,
                Address = appAddr,
                Port = port,
                Tags = new [] { "api" },
                Check = new ConsulCheck
                {
                    HTTP = $"http://{appAddr}:{port}/health",
                    Method = "GET",
                    Interval = "10s",
                    DeregisterCriticalServiceAfter = "1m"
                }
            };

            var response = await client.PutAsJsonAsync("/v1/agent/service/register", registration, ct);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Registered {Service} at {Address}:{Port} with ID {Id}", appName, appAddr, port, _serviceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register service in Consul");
        }
    }

    public async Task DeregisterAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_serviceId)) return;
        try
        {
            var consul = consulOpts.Value.Address.TrimEnd('/');
            var client = httpClientFactory.CreateClient("consul");
            client.BaseAddress = new Uri(consul);
            var resp = await client.PutAsync($"/v1/agent/service/deregister/{_serviceId}", content: null, ct);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deregister service {Id}", _serviceId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ConsulDiscoveryClient(IHttpClientFactory httpClientFactory, IOptions<ServiceDefaults.ConsulOptions> consulOpts)  // ← fully qualified
{
    public async Task<IReadOnlyList<(string Address,int Port)>> GetHealthyInstancesAsync(string serviceName, CancellationToken ct = default)
    {
        var baseAddr = consulOpts.Value.Address.TrimEnd('/');
        var http = httpClientFactory.CreateClient("consul");
        http.BaseAddress = new Uri(baseAddr);

        var url = $"/v1/health/service/{serviceName}?passing=true";
        var json = await http.GetFromJsonAsync<List<dynamic>>(url, ct) ?? new();
        var results = new List<(string,int)>();
        foreach (var item in json)
        {
            try
            {
                string addr = item["Service"]["Address"].ToString();
                int port = int.Parse(item["Service"]["Port"].ToString());
                results.Add((addr, port));
            }
            catch { /* ignore malformed entries */ }
        }
        return results;
    }
}
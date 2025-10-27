// ServiceDefaults/ServiceDefaults.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    IOptions<ConsulOptions> consulOpts,
    IHostApplicationLifetime lifetime,
    ILogger<ConsulRegistrationHostedService> logger) : IHostedService
{
    private string? _serviceId;

    public async Task StartAsync(CancellationToken ct)
    {
        var name = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "unknown";
        var host = Environment.GetEnvironmentVariable("SERVICE_HOST") ?? "localhost";
        if (!int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var port) || port <= 0)
        { logger.LogError("Invalid SERVICE_PORT"); return; }

        var hostname = Dns.GetHostName();
        _serviceId = $"{name}-{hostname}";

        try
        {
            var baseUrl = consulOpts.Value.Address.TrimEnd('/');
            var http = httpClientFactory.CreateClient();
            http.BaseAddress = new Uri(baseUrl);

            try { await http.PutAsync($"/v1/agent/service/deregister/{_serviceId}", null, ct); } catch {}

            var reg = new ConsulServiceRegistration
            {
                ID = _serviceId,
                Name = name,
                Address = host,
                Port = port,
                Tags = new[] { "api" },
                Check = new ConsulCheck
                {
                    HTTP = $"http://{host}:{port}/health",
                    Method = "GET",
                    Interval = "10s",
                    DeregisterCriticalServiceAfter = "1m"
                }
            };

            var resp = await http.PutAsJsonAsync("/v1/agent/service/register", reg, ct);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Consul registration failed");
        }

        lifetime.ApplicationStopping.Register(() => _ = DeregisterAsync(CancellationToken.None));
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task DeregisterAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_serviceId)) return;
        try
        {
            var http = httpClientFactory.CreateClient();
            http.BaseAddress = new Uri(consulOpts.Value.Address.TrimEnd('/'));
            await http.PutAsync($"/v1/agent/service/deregister/{_serviceId}", null, ct);
        } catch {}
    }
}

public sealed class ConsulDiscoveryClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ConsulOptions> consulOpts)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<(string Address, int Port)>> GetHealthyInstancesAsync(
        string serviceName, CancellationToken ct = default)
    {
        var http = httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(consulOpts.Value.Address.TrimEnd('/'));

        var url = $"/v1/health/service/{serviceName}?passing=true";
        var data = await http.GetFromJsonAsync<List<HealthEntry>>(url, JsonOpts, ct) ?? new();

        return data
            .Where(e => e.Service is not null)
            .Select(e => (e.Service.Address ?? "localhost", e.Service.Port))
            .ToList();
    }

    private sealed class HealthEntry { public EntryService Service { get; set; } = new(); }
    private sealed class EntryService { public string Address { get; set; } = ""; public int Port { get; set; } }
}

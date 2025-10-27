
using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ServiceDefaults;

public static class ServiceSetup
{
    public static void AddServiceDefaults(this HostApplicationBuilder builder, string serviceName)
    {
        // === Global config layering ===
        var contentRoot = builder.Environment.ContentRootPath;
        var defaultSharedPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "_global", "appsettings.shared.json"));
        var sharedPath = Environment.GetEnvironmentVariable("GLOBAL_CONFIG_PATH") ?? defaultSharedPath;
        builder.Configuration.AddJsonFile(sharedPath, optional: true, reloadOnChange: true);

        // Azure App Config optional
        var endpoint = builder.Configuration["Global:AppConfig:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.Configuration.AddAzureAppConfiguration(options =>
            {
                options.Connect(new Uri(endpoint), new DefaultAzureCredential())
                       .Select(keyFilter: KeyFilter.Any, label: builder.Environment.EnvironmentName)
                       .Select(keyFilter: KeyFilter.Any, label: "global")
                       .ConfigureRefresh(refresh =>
                       {
                           refresh.Register(key: "sentinel", label: "global", refreshAll: true)
                                  .SetCacheExpiration(TimeSpan.FromSeconds(30));
                       })
                       .UseFeatureFlags(ff => ff.CacheExpirationInterval = TimeSpan.FromSeconds(30));
            });
        }

        // Options
        builder.Services.AddValidatedOptions<AppLimits>("App:Limits");
        builder.Services.AddFeatureManagement();

        // === OpenTelemetry (console exporter only, no infra needed) ===
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: "1.0.0"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("ServiceDefaults.Correlation") // custom source if needed
                .AddConsoleExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        // === Correlation ID ===
        builder.Services.AddScoped<CorrelationIdMiddleware>();

        // === Rate Limiting (fixed window) ===
        builder.Services.AddRateLimiter(_ => _
            .AddFixedWindowLimiter(policyName: "fixed", options =>
            {
                options.PermitLimit = 100;        // 100 req/window
                options.Window = TimeSpan.FromSeconds(1);
                options.QueueLimit = 0;
            }));
    }

    public static void UseServiceDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseRateLimiter();
    }
}

public class CorrelationIdMiddleware : IMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger) => _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var cid = context.Request.Headers.TryGetValue(HeaderName, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = cid;
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = cid }))
        {
            await next(context);
        }
    }
}

public static class OptionsHelpers
{
    public static IServiceCollection AddValidatedOptions<TOptions>(this IServiceCollection services, string section)
        where TOptions : class, new()
    {
        services.AddOptions<TOptions>()
                .BindConfiguration(section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        return services;
    }
}

public class AppLimits
{
    [Range(1, 10000)]
    public int MaxRequestsPerSecond { get; set; } = 100;
}

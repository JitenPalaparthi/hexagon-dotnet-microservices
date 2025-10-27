using Microsoft.FeatureManagement;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Load global config
var globalPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "_global", "appsettings.shared.json"));
builder.Configuration.AddJsonFile(globalPath, optional: true, reloadOnChange: true);

// 2️⃣ Add Feature Management
builder.Services.AddFeatureManagement();

// 3️⃣ Bind strongly typed


// 1️⃣ Load global config
builder.Configuration.AddJsonFile(globalPath, optional: true, reloadOnChange: true);

// 2️⃣ Add Feature Management
builder.Services.AddFeatureManagement();

// 3️⃣ Bind strongly typed options
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

var app = builder.Build();


app.MapGet("/", (IConfiguration cfg, IOptions<AppSettings> opts) => new
{
    Company = cfg["Company:Name"],
    Environment = cfg["Company:Environment"],
    Service = opts.Value.ServiceName,
    Timestamp = DateTime.UtcNow
});

app.MapGet("/welcome", async (IFeatureManager fm) =>
{
    return await fm.IsEnabledAsync("ShowWelcome")
        ? Results.Ok("👋 Welcome! Feature flag is ON")
        : Results.Ok("Welcome hidden (flag OFF)");
});

app.MapGet("/discount", async (IFeatureManager fm) =>
{
    return await fm.IsEnabledAsync("EnableDiscountBanner")
        ? Results.Ok("🎉 Discount banner ENABLED!")
        : Results.Ok("Discount banner DISABLED");
});

app.Run();

public class AppSettings
{
    public string ServiceName { get; set; } = "DemoApp";
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? "Server=localhost,1433;Database=JwtDemoDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;";

// Minimal EF Core DbContext
builder.Services.AddDbContext<AppDb>(opt => opt.UseSqlServer(connectionString));

// Pull JWT secret from Vault (dev) at startup
builder.Services.AddSingleton<IJwtSecretProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<VaultJwtSecretProvider>>();
    var addr   = Environment.GetEnvironmentVariable("VAULT_ADDR")    ?? "http://localhost:8200";
    var token  = Environment.GetEnvironmentVariable("VAULT_TOKEN")   ?? "root";
    var path   = Environment.GetEnvironmentVariable("VAULT_SECRET_PATH") ?? "secret/data/jwt";
    return new VaultJwtSecretProvider(addr, token, path, logger);
});

// Configure JWT auth (will resolve secret from provider at runtime)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx => Task.CompletedTask
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                var prov = builder.Services.BuildServiceProvider().GetRequiredService<IJwtSecretProvider>();
                var secret = prov.GetSecretAsync().GetAwaiter().GetResult();
                return new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)) };
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapPost("/register", async (RegisterDto dto, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { error = "Username and Password required" });

    if (await db.Users.AnyAsync(u => u.Username == dto.Username))
        return Results.Conflict(new { error = "Username already exists" });

    var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 11);
    db.Users.Add(new User { Username = dto.Username, PasswordHash = hash });
    await db.SaveChangesAsync();
    return Results.Created($"/users/{dto.Username}", new { ok = true });
});

app.MapPost("/login", async (LoginDto dto, AppDb db, IJwtSecretProvider prov) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
    if (user is null) return Results.Unauthorized();
    if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        return Results.Unauthorized();

    var secret = await prov.GetSecretAsync();
    var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Username),
        new Claim("uid", user.Id.ToString())
    };
    var jwt = new JwtSecurityToken(
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.AddHours(2),
        signingCredentials: creds);
    var token = new JwtSecurityTokenHandler().WriteToken(jwt);
    return Results.Ok(new { token });
});

app.MapGet("/me", async (ClaimsPrincipal user, AppDb db) =>
{
    var name = user.Identity?.Name ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    var uid = user.FindFirstValue("uid");
    if (uid is null) return Results.Unauthorized();

    var id = int.Parse(uid);
    var u = await db.Users.FindAsync(id);
    if (u is null) return Results.NotFound();
    return Results.Ok(new { username = u.Username, id = u.Id });
}).RequireAuthorization();

app.UseAuthentication();
app.UseAuthorization();
app.Run();

// --- models / db ---
public record RegisterDto(string Username, string Password);
public record LoginDto(string Username, string Password);

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
}

// --- Vault secret provider (simple HTTP client for dev) ---
public interface IJwtSecretProvider
{
    Task<string> GetSecretAsync();
}

public class VaultJwtSecretProvider : IJwtSecretProvider
{
    private readonly string _addr;
    private readonly string _token;
    private readonly string _path;
    private readonly ILogger _logger;
    private string? _cached;

    public VaultJwtSecretProvider(string addr, string token, string path, ILogger logger)
    {
        _addr = addr.TrimEnd('/');
        _token = token;
        _path = path; // e.g., secret/data/jwt
        _logger = logger;
    }

    public async Task<string> GetSecretAsync()
    {
        if (!string.IsNullOrEmpty(_cached)) return _cached!;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var req = new HttpRequestMessage(HttpMethod.Get, $"{_addr}/v1/{_path}");
        req.Headers.Add("X-Vault-Token", _token);
        var resp = await http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogError("Vault read failed: {Status} {Body}", resp.StatusCode, body);
            throw new InvalidOperationException($"Vault read failed: {resp.StatusCode}");
        }
        var json = await resp.Content.ReadAsStringAsync();
        // Expect KV v2: { data: { data: { secret: "..." }, metadata: { ... } } }
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("data", out var inner)
            && inner.TryGetProperty("secret", out var secretProp))
        {
            _cached = secretProp.GetString() ?? throw new Exception("secret missing");
            _logger.LogInformation("Loaded JWT secret from Vault path {Path}", _path);
            return _cached!;
        }
        throw new InvalidOperationException("Vault response missing data.data.secret");
    }
}

using ConSecOrg.Application;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Infrastructure;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Server.Hubs;
using ConSecOrg.Server.Infrastructure;
using ConSecOrg.Server.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ── Layers ───────────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── HTTP context & current user ───────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtSecret = "DevOnlyConSecOrgSecretKey2024Development!!";
        Log.Warning("JwtSettings:Secret not set — using dev fallback. NOT FOR PRODUCTION.");
    }
    else
        throw new InvalidOperationException("JwtSettings:Secret is not set. Use dotnet user-secrets.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        // Allow SignalR to use token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 5;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });

    options.AddSlidingWindowLimiter("api", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow = 6;
        o.PermitLimit = 200;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
});

// ── Controllers + SignalR + OpenAPI ──────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Force UTC "Z" suffix on all DateTime values so clients with non-UTC
        // timezones (e.g. UTC+3) do not experience a 3-hour display offset.
        options.JsonSerializerOptions.Converters.Add(new ConSecOrg.Server.Infrastructure.UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ConSecOrg.Server.Infrastructure.UtcNullableDateTimeJsonConverter());
    });
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ── Автоматическое применение всех ожидающих миграций ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            Log.Information("Applying {Count} pending migration(s): {Names}",
                pending.Count, string.Join(", ", pending));
            db.Database.Migrate();
            Log.Information("All migrations applied successfully.");
        }
        else
        {
            Log.Information("Database schema is up to date.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to apply migrations: {Error}", ex.Message);
    }
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();   // доступен на /openapi/v1.json

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseStaticFiles(); // serves wwwroot/uploads/* for file attachments
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<DeviceValidationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<BoardHub>("/hubs/board");

app.Run();

public partial class Program { }

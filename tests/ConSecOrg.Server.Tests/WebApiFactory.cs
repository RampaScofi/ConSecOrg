using ConSecOrg.Application.Features.Auth.Commands;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Server.Services;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace ConSecOrg.Server.Tests;

/// <summary>
/// Фабрика тестового сервера — заменяет SQL Server на EF Core InMemory
/// и отключает привязку устройства, чтобы тесты не зависели от реальной БД.
/// </summary>
public class WebApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"ConSecOrg_Test_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Disable device binding so tests don't need matching fingerprints
                ["SecuritySettings:RequireDeviceBinding"] = "false"
            }));

        // ConfigureTestServices runs AFTER Program.cs (all app services already registered).
        // In EF Core 10, multiple AddDbContext calls ACCUMULATE option configurations via
        // IDbContextOptionsConfiguration<T>. To avoid "two providers" conflict, we bypass
        // the AddDbContext factory entirely and register DbContextOptions<T> directly as a
        // singleton — this way only the InMemory options are ever applied.
        builder.ConfigureTestServices(services =>
        {
            // Remove the AppDbContext and its options registered by AddInfrastructure
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Register fresh InMemory options directly (no AddDbContext accumulation)
            services.AddSingleton(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options);
            services.AddScoped<AppDbContext>();

            // Remove background service — keeps tests deterministic
            var expiration = services
                .FirstOrDefault(d => d.ImplementationType == typeof(NoteExpirationService));
            if (expiration is not null) services.Remove(expiration);
        });
    }

    /// <summary>Seeds the four standard roles. Safe to call multiple times.</summary>
    public void SeedRoles()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        if (db.Roles.Any()) return;

        const string permsAll = """{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u","d"],"audit":["r"],"users":["r","c","u","d"]}""";
        const string permsManager = """{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u"],"audit":[],"users":["r"]}""";
        const string permsAuditor = """{"notes":["r"],"tasks":["r"],"contacts":[],"audit":["r"],"users":["r"]}""";
        const string permsUser = """{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u","d"],"audit":[],"users":[]}""";

        db.Roles.AddRange(
            new Role(Guid.NewGuid(), "Admin", permsAll),
            new Role(Guid.NewGuid(), "Manager", permsManager),
            new Role(Guid.NewGuid(), "Auditor", permsAuditor),
            new Role(Guid.NewGuid(), "User", permsUser));
        db.SaveChanges();
    }

    /// <summary>
    /// Registers a user via the /auth/register-self endpoint and logs in,
    /// returning an HttpClient with the Bearer token already set.
    /// </summary>
    public async Task<(HttpClient AuthClient, string AccessToken)> CreateAuthenticatedClientAsync(
        string? username = null, string? password = null)
    {
        SeedRoles();
        var client = CreateClient();

        username ??= "user_" + Guid.NewGuid().ToString("N")[..8];
        password ??= "TestPass@2026";

        await client.PostAsJsonAsync("/api/v1/auth/register-self",
            new { Username = username, Email = $"{username}@test.com", Password = password, RoleId = "" });

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = username, Password = password, DeviceFingerprint = (string?)null });

        loginResp.EnsureSuccessStatusCode();
        var tokens = await loginResp.Content.ReadFromJsonAsync<LoginResponseHelper>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return (client, tokens.AccessToken);
    }

    // ── Admin bootstrap ────────────────────────────────────────────────────────

    private const string AdminUsername = "admin_test";
    private const string AdminPassword = "AdminPass@2026";
    private bool _adminBootstrapped;
    private readonly SemaphoreSlim _adminLock = new(1, 1);

    /// <summary>
    /// Ensures an Admin user exists and returns an authenticated HttpClient with Admin role.
    /// Uses the service layer directly to avoid dependency on other HTTP tests having run first.
    /// </summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        SeedRoles();

        await _adminLock.WaitAsync();
        try
        {
            if (!_adminBootstrapped)
            {
                using var scope = Services.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var adminRole = db.Roles.First(r => r.Name == "Admin");

                try
                {
                    await sender.Send(new RegisterCommand(
                        AdminUsername, "admin@test.com", AdminPassword, adminRole.Id));
                }
                catch
                {
                    // Ignore: user already exists (another test may have created them)
                }

                _adminBootstrapped = true;
            }
        }
        finally
        {
            _adminLock.Release();
        }

        var client = CreateClient();
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = AdminUsername, Password = AdminPassword, DeviceFingerprint = (string?)null });
        loginResp.EnsureSuccessStatusCode();

        var tokens = await loginResp.Content.ReadFromJsonAsync<LoginResponseHelper>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }

    // Minimal helper DTO — avoids pulling ConSecOrg.Shared into the factory itself
    private sealed class LoginResponseHelper
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}

using System.Net;
using System.Net.Http.Json;
using ConSecOrg.Shared.DTOs.Auth;
using FluentAssertions;

namespace ConSecOrg.Server.Tests;

public class AuthControllerTests(WebApiFactory factory) : IClassFixture<WebApiFactory>
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private HttpClient NewClient() => factory.CreateClient();

    private static string UniqueUser() => "u_" + Guid.NewGuid().ToString("N")[..10];

    private async Task<(string Username, string Password)> RegisterAsync(HttpClient client)
    {
        factory.SeedRoles();
        var username = UniqueUser();
        const string password = "TestPass@2026";

        var r = await client.PostAsJsonAsync("/api/v1/auth/register-self",
            new RegisterRequestDto { Username = username, Email = $"{username}@t.com", Password = password });
        r.IsSuccessStatusCode.Should().BeTrue($"register failed: {await r.Content.ReadAsStringAsync()}");

        return (username, password);
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterSelf_ValidData_Returns201()
    {
        factory.SeedRoles();
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register-self",
            new RegisterRequestDto
            {
                Username = UniqueUser(),
                Email = $"{UniqueUser()}@example.com",
                Password = "SecurePass@123"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RegisterSelf_DuplicateUsername_Returns400()
    {
        factory.SeedRoles();
        var client = NewClient();
        var username = UniqueUser();

        await client.PostAsJsonAsync("/api/v1/auth/register-self",
            new RegisterRequestDto { Username = username, Email = $"a_{username}@t.com", Password = "Pass1@2026" });

        var r2 = await client.PostAsJsonAsync("/api/v1/auth/register-self",
            new RegisterRequestDto { Username = username, Email = $"b_{username}@t.com", Password = "Pass2@2026" });

        // RegisterCommand throws ValidationException for duplicate → 400 Bad Request
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var client = NewClient();
        var (username, password) = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequestDto { Username = username, Password = password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Username.Should().Be(username);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = NewClient();
        var (username, _) = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequestDto { Username = username, Password = "WrongPass!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns404()
    {
        factory.SeedRoles();
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequestDto { Username = "no_such_user_xyz", Password = "SomePass" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await NewClient().GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUserInfo()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var info = await response.Content.ReadFromJsonAsync<UserInfoDto>();
        info.Should().NotBeNull();
        info!.Username.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Logout_WithValidToken_Returns204()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

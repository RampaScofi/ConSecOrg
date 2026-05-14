using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ConSecOrg.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    private const string FallbackSecret = "ConSecOrgFallbackKey2026!!SecureOrganizerDiploma";

    private string GetSecret() =>
        _config["JwtSettings:Secret"] is { Length: > 0 } s ? s : FallbackSecret;

    public string GenerateAccessToken(User user, Guid sessionId)
    {
        var secret = GetSecret();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = int.Parse(_config["JwtSettings:AccessTokenExpiryMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("username", user.Username),
            new Claim(ClaimTypes.Role, user.Role?.Name ?? "User"),
            new Claim("session_id", sessionId.ToString()),
            new Claim("device_id", user.DeviceId ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));

    public Guid? ValidateRefreshToken(string token)
    {
        // Refresh token — просто Base64 случайные байты; sessionId хранится в БД.
        // Этот метод не используется напрямую — проверка идёт через GetSessionByTokenHashAsync.
        return null;
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var secret = GetSecret();
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _config["JwtSettings:Issuer"],
                ValidAudience = _config["JwtSettings:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch { return null; }
    }
}

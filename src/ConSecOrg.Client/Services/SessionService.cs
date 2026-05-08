using ConSecOrg.Shared.DTOs.Auth;
using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Client.Services;

public sealed class SessionService
{
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _accessTokenExpiry;
    private byte[]? _encryptionKey;

    public UserInfoDto? CurrentUser { get; private set; }
    // True for both corporate (JWT) and personal (PIN) sessions
    public bool IsAuthenticated => CurrentUser is not null;
    // True only when a valid JWT is present (corporate mode)
    public bool HasValidToken => CurrentUser is not null && _accessToken is not null;

    public string? AccessToken => IsTokenValid() ? _accessToken : null;

    public event Action? UserChanged;

    public void SetSession(LoginResponseDto response, byte[] keyMaterial)
    {
        _accessToken = response.AccessToken;
        _refreshToken = response.RefreshToken;
        _accessTokenExpiry = response.AccessTokenExpiry;
        CurrentUser = response.User;

        if (keyMaterial.Length >= 64)
        {
            _encryptionKey = new byte[64];
            Buffer.BlockCopy(keyMaterial, 0, _encryptionKey, 0, 64);
        }

        UserChanged?.Invoke();
    }

    public void SetPersonalSession(Guid userId, byte[] keyMaterial64)
    {
        _accessToken = null;
        _refreshToken = null;
        _accessTokenExpiry = DateTime.MaxValue;
        CurrentUser = new UserInfoDto
        {
            Id = userId,
            Username = "Я",
            Email = string.Empty,
            Role = UserRoleDto.User
        };

        if (_encryptionKey is not null)
            Array.Clear(_encryptionKey, 0, _encryptionKey.Length);

        _encryptionKey = new byte[64];
        Buffer.BlockCopy(keyMaterial64, 0, _encryptionKey, 0, 64);
        Array.Clear(keyMaterial64, 0, keyMaterial64.Length);

        UserChanged?.Invoke();
    }

    public void UpdateTokens(string accessToken, DateTime expiry, string refreshToken)
    {
        _accessToken = accessToken;
        _accessTokenExpiry = expiry;
        _refreshToken = refreshToken;
    }

    public string? GetRefreshToken() => _refreshToken;

    public byte[]? GetEncryptionKey() => _encryptionKey;

    public void Clear()
    {
        _accessToken = null;
        _refreshToken = null;
        CurrentUser = null;
        if (_encryptionKey is not null)
        {
            Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
            _encryptionKey = null;
        }
        UserChanged?.Invoke();
    }

    private bool IsTokenValid() =>
        _accessToken is not null && DateTime.UtcNow < _accessTokenExpiry.AddMinutes(-2);
}

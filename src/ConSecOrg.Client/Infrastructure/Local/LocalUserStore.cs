using ConSecOrg.Infrastructure.Crypto;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConSecOrg.Client.Infrastructure.Local;

internal sealed class LocalUserConfig
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("pinSalt")]
    public string PinSalt { get; set; } = string.Empty;

    [JsonPropertyName("pinVerifier")]
    public string PinVerifier { get; set; } = string.Empty;
}

public sealed class LocalUserStore
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConSecOrg");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "local_config.json");

    private readonly KdfService _kdf = new();
    private LocalUserConfig _config = new();

    public bool HasPin => !string.IsNullOrEmpty(_config.PinVerifier);
    public Guid UserId => _config.UserId;

    public LocalUserStore()
    {
        Load();
    }

    private void Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                _config = JsonSerializer.Deserialize<LocalUserConfig>(json) ?? new();
            }
            catch { _config = new(); }
        }

        if (_config.UserId == Guid.Empty)
        {
            _config.UserId = Guid.NewGuid();
            Save();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public void CreatePin(string pin)
    {
        var salt = _kdf.GenerateSalt();
        var derived = _kdf.Derive(pin, salt);

        var verifier = new byte[32];
        Buffer.BlockCopy(derived, 0, verifier, 0, 32);
        Array.Clear(derived, 0, derived.Length);

        _config.PinSalt = Convert.ToBase64String(salt);
        _config.PinVerifier = Convert.ToBase64String(verifier);
        Save();
    }

    public bool VerifyPin(string pin)
    {
        if (!HasPin) return false;
        var salt = Convert.FromBase64String(_config.PinSalt);
        var derived = _kdf.Derive(pin, salt);

        var verifier = new byte[32];
        Buffer.BlockCopy(derived, 0, verifier, 0, 32);
        Array.Clear(derived, 0, derived.Length);

        var stored = Convert.FromBase64String(_config.PinVerifier);
        return CryptographicOperations.FixedTimeEquals(verifier, stored);
    }

    public byte[] DeriveKey(string pin)
    {
        var salt = Convert.FromBase64String(_config.PinSalt);
        return _kdf.Derive(pin, salt); // 64 bytes: [0..31] cipher key, [32..63] HMAC key
    }

    public void ResetPin()
    {
        _config.PinSalt = string.Empty;
        _config.PinVerifier = string.Empty;
        Save();
    }
}

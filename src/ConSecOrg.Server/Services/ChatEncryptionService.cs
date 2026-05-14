using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;
using System.Text;

namespace ConSecOrg.Server.Services;

/// <summary>
/// Server-side ГОСТ encryption for chat messages at rest.
/// Uses a 64-byte key derived from ChatSettings:EncryptionSeed via Streebog-512.
/// </summary>
public sealed class ChatEncryptionService
{
    private readonly byte[] _key;
    private readonly ICryptoService _crypto;

    public ChatEncryptionService(IConfiguration config, ICryptoService crypto)
    {
        _crypto = crypto;
        var seed = config.GetValue<string>("ChatSettings:EncryptionSeed")
            ?? "ConSecOrg-Default-Chat-Encryption-Seed-2026!!";
        // Derive 64-byte key: Streebog-512 of the seed string
        _key = _crypto.Hash512(Encoding.UTF8.GetBytes(seed));
    }

    public (byte[] cipher, byte[] nonce, byte[] hmac) Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return ([], [], []);
        var ec = _crypto.Encrypt(Encoding.UTF8.GetBytes(plainText), _key);
        return (ec.CipherText, ec.Nonce, ec.Hmac);
    }

    public string Decrypt(byte[] cipher, byte[] nonce, byte[] hmac)
    {
        try
        {
            var ec = new EncryptedContent(cipher, nonce, hmac);
            var plainBytes = _crypto.Decrypt(ec, _key);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return "[ошибка расшифровки]";
        }
    }
}

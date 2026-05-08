using ConSecOrg.Domain.Exceptions;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;
using System.Security.Cryptography;

namespace ConSecOrg.Infrastructure.Crypto;

// Implements GOST R 34.12-2015 "Kuznechik" CTR + GOST R 34.11-2012 "Streebog-256" HMAC
public sealed class GostCryptoService : ICryptoService
{
    private readonly StreebogHasher _hasher = new();
    private readonly HmacStreebog _hmac = new();

    public EncryptedContent Encrypt(byte[] plaintext, byte[] encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ValidateKey(encryptionKey);

        var (cryptKey, hmacKey) = SplitKey(encryptionKey);

        // Generate unique 128-bit nonce for this record
        var nonce = RandomNumberGenerator.GetBytes(16);

        // Encrypt with Kuznechik in CTR mode (GOST R 34.12-2015)
        var cipherText = KuznechikEngine.CtrProcess(plaintext, cryptKey, nonce);

        // Compute HMAC-Streebog-256 over nonce || ciphertext (Encrypt-then-MAC)
        var hmacInput = Combine(nonce, cipherText);
        var mac = _hmac.Compute(hmacInput, hmacKey);

        Array.Clear(cryptKey, 0, cryptKey.Length);
        Array.Clear(hmacKey, 0, hmacKey.Length);

        return new EncryptedContent(cipherText, nonce, mac);
    }

    public byte[] Decrypt(EncryptedContent content, byte[] encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateKey(encryptionKey);

        var (cryptKey, hmacKey) = SplitKey(encryptionKey);

        // VERIFY HMAC BEFORE DECRYPTION — authenticate-then-decrypt
        var hmacInput = Combine(content.Nonce, content.CipherText);
        if (!_hmac.Verify(hmacInput, hmacKey, content.Hmac))
        {
            Array.Clear(cryptKey, 0, cryptKey.Length);
            Array.Clear(hmacKey, 0, hmacKey.Length);
            throw new IntegrityViolationException();
        }

        var plaintext = KuznechikEngine.CtrProcess(content.CipherText, cryptKey, content.Nonce);

        Array.Clear(cryptKey, 0, cryptKey.Length);
        Array.Clear(hmacKey, 0, hmacKey.Length);

        return plaintext;
    }

    public byte[] Hash256(byte[] data) => _hasher.Hash256(data);

    public byte[] Hash512(byte[] data) => _hasher.Hash512(data);

    public byte[] ComputeHmac(byte[] data, byte[] key) => _hmac.Compute(data, key);

    public bool VerifyHmac(byte[] data, byte[] key, byte[] expectedHmac) => _hmac.Verify(data, key, expectedHmac);

    private static (byte[] cryptKey, byte[] hmacKey) SplitKey(byte[] keyMaterial)
    {
        // keyMaterial is 64 bytes: [0..31] = Kuznechik key, [32..63] = HMAC key
        var cryptKey = new byte[32];
        var hmacKey = new byte[32];
        Buffer.BlockCopy(keyMaterial, 0, cryptKey, 0, 32);
        Buffer.BlockCopy(keyMaterial, 32, hmacKey, 0, 32);
        return (cryptKey, hmacKey);
    }

    private static void ValidateKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 64)
            throw new ArgumentException("Key material must be 64 bytes (32 Kuznechik + 32 HMAC).", nameof(key));
    }

    private static byte[] Combine(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        return result;
    }
}

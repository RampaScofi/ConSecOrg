using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.ValueObjects;

public sealed class EncryptedContent : ValueObject
{
    public byte[] CipherText { get; }
    public byte[] Nonce { get; }      // 16 bytes (128-bit IV for Kuznechik CTR)
    public byte[] Hmac { get; }       // 32 bytes (HMAC-Streebog-256)
    public string AlgorithmId { get; }

    public static readonly string DefaultAlgorithm = "GOST-R-34.12-2015/CTR";

    public EncryptedContent(byte[] cipherText, byte[] nonce, byte[] hmac, string? algorithmId = null)
    {
        ArgumentNullException.ThrowIfNull(cipherText);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(hmac);
        if (nonce.Length != 16) throw new ArgumentException("Nonce must be 16 bytes.", nameof(nonce));
        if (hmac.Length != 32) throw new ArgumentException("HMAC must be 32 bytes.", nameof(hmac));

        CipherText = cipherText;
        Nonce = nonce;
        Hmac = hmac;
        AlgorithmId = algorithmId ?? DefaultAlgorithm;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Convert.ToBase64String(CipherText);
        yield return Convert.ToBase64String(Nonce);
        yield return Convert.ToBase64String(Hmac);
        yield return AlgorithmId;
    }
}

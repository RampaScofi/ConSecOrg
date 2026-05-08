using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace ConSecOrg.Infrastructure.Crypto;

public sealed class HmacStreebog
{
    // HMAC-Streebog-256: 32-byte output
    public byte[] Compute(byte[] data, byte[] key)
    {
        var hmac = new HMac(new Gost3411_2012_256Digest());
        hmac.Init(new KeyParameter(key));
        hmac.BlockUpdate(data, 0, data.Length);
        var result = new byte[hmac.GetMacSize()];
        hmac.DoFinal(result, 0);
        return result;
    }

    public byte[] Compute(byte[][] parts, byte[] key)
    {
        var hmac = new HMac(new Gost3411_2012_256Digest());
        hmac.Init(new KeyParameter(key));
        foreach (var part in parts)
            hmac.BlockUpdate(part, 0, part.Length);
        var result = new byte[hmac.GetMacSize()];
        hmac.DoFinal(result, 0);
        return result;
    }

    public bool Verify(byte[] data, byte[] key, byte[] expectedHmac)
    {
        var computed = Compute(data, key);
        return CryptographicEquals(computed, expectedHmac);
    }

    // Constant-time comparison to prevent timing attacks
    private static bool CryptographicEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

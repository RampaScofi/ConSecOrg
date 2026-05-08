using ConSecOrg.Domain.Interfaces.Services;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;
using System.Text;

namespace ConSecOrg.Infrastructure.Crypto;

public sealed class KdfService : IKdfService
{
    private const int DefaultIterations = 100_000;
    private const int OutputLength = 64; // 32 encryption key + 32 HMAC key

    public byte[] Derive(string password, byte[] salt, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(salt);

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return Pbkdf2HmacStreebog256(passwordBytes, salt, iterations, OutputLength);
        }
        finally
        {
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
        }
    }

    public byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(32);

    // Manual PBKDF2 implementation per RFC 2898, PRF = HMAC-Streebog-256
    private static byte[] Pbkdf2HmacStreebog256(byte[] password, byte[] salt, int iterations, int dkLen)
    {
        var hmac = new HMac(new Gost3411_2012_256Digest());
        int hLen = hmac.GetMacSize(); // 32 bytes for Streebog-256

        int blocks = (int)Math.Ceiling((double)dkLen / hLen);
        var dk = new byte[dkLen];

        for (int i = 1; i <= blocks; i++)
        {
            hmac.Init(new KeyParameter(password));

            // U_1 = PRF(Password, Salt || INT(i))
            var u = new byte[hLen];
            hmac.BlockUpdate(salt, 0, salt.Length);
            var idx = new[] { (byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i };
            hmac.BlockUpdate(idx, 0, 4);
            hmac.DoFinal(u, 0);

            var t = (byte[])u.Clone();

            // U_2 .. U_c
            for (int j = 2; j <= iterations; j++)
            {
                hmac.Reset();
                hmac.BlockUpdate(u, 0, hLen);
                hmac.DoFinal(u, 0);
                for (int k = 0; k < hLen; k++) t[k] ^= u[k];
            }

            int offset = (i - 1) * hLen;
            int count = Math.Min(hLen, dkLen - offset);
            Buffer.BlockCopy(t, 0, dk, offset, count);
        }

        return dk;
    }
}

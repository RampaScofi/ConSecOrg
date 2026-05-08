using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Infrastructure.Crypto;

namespace ConSecOrg.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly KdfService _kdf = new();

    public (byte[] Hash, byte[] Salt) Hash(string password)
    {
        var salt = _kdf.GenerateSalt();
        var km = _kdf.Derive(password, salt);

        // km[0..31] = verifier stored in DB
        // km[32..63] = encryption key (NOT stored)
        var verifier = new byte[32];
        Buffer.BlockCopy(km, 0, verifier, 0, 32);
        Array.Clear(km, 0, km.Length);

        return (verifier, salt);
    }

    public (bool IsValid, byte[] KeyMaterial) Verify(string password, byte[] storedHash, byte[] salt)
    {
        var km = _kdf.Derive(password, salt);

        bool valid = CryptographicEquals(km.AsSpan(0, 32), storedHash);

        if (!valid)
        {
            Array.Clear(km, 0, km.Length);
            return (false, []);
        }

        return (true, km);
    }

    private static bool CryptographicEquals(Span<byte> a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

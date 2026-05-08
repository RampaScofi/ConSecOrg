using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.ValueObjects;

public sealed class PasswordHash : ValueObject
{
    public byte[] Hash { get; }   // 32 bytes — stored verifier (PBKDF2 output first half)
    public byte[] Salt { get; }   // 32 bytes — random salt

    public PasswordHash(byte[] hash, byte[] salt)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(salt);
        if (hash.Length != 32) throw new ArgumentException("Hash must be 32 bytes.", nameof(hash));
        if (salt.Length != 32) throw new ArgumentException("Salt must be 32 bytes.", nameof(salt));
        Hash = hash;
        Salt = salt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Convert.ToBase64String(Hash);
        yield return Convert.ToBase64String(Salt);
    }
}

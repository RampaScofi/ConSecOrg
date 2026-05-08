using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.ValueObjects;

public sealed class HashChainEntry : ValueObject
{
    public byte[] PreviousHash { get; }  // 32 bytes — Streebog-256 of previous audit record
    public byte[] CurrentHash { get; }   // 32 bytes — Streebog-256(prev || timestamp || action || entityId || userId)

    public static readonly byte[] GenesisHash = new byte[32]; // all zeros for first entry

    public HashChainEntry(byte[] previousHash, byte[] currentHash)
    {
        ArgumentNullException.ThrowIfNull(previousHash);
        ArgumentNullException.ThrowIfNull(currentHash);
        if (previousHash.Length != 32) throw new ArgumentException("PreviousHash must be 32 bytes.", nameof(previousHash));
        if (currentHash.Length != 32) throw new ArgumentException("CurrentHash must be 32 bytes.", nameof(currentHash));
        PreviousHash = previousHash;
        CurrentHash = currentHash;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Convert.ToBase64String(PreviousHash);
        yield return Convert.ToBase64String(CurrentHash);
    }
}

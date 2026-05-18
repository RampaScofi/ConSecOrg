using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Crypto;
using System.Text;

namespace ConSecOrg.Infrastructure.Security;

public sealed class HashChainService : IHashChainService
{
    private readonly StreebogHasher _hasher = new();

    public HashChainEntry ComputeEntry(byte[]? previousHash, AuditLog currentData)
    {
        var prev = previousHash ?? HashChainEntry.GenesisHash;

        // Hash = Streebog-256(previousHash || timestamp || action || entityId || userId)
        // SpecifyKind ensures ToString("O") always appends "Z", matching DateTime.UtcNow
        // used at write time (EF Core reads DateTime back as Kind=Unspecified from SQL Server).
        var parts = new[]
        {
            prev,
            Encoding.UTF8.GetBytes(DateTime.SpecifyKind(currentData.Timestamp, DateTimeKind.Utc).ToString("O")),
            Encoding.UTF8.GetBytes(currentData.Action.ToString()),
            Encoding.UTF8.GetBytes(currentData.EntityId ?? ""),
            Encoding.UTF8.GetBytes(currentData.UserId?.ToString() ?? "")
        };

        var combined = parts.SelectMany(p => p).ToArray();
        var current = _hasher.Hash256(combined);

        return new HashChainEntry(prev, current);
    }

    public (bool Ok, long? TamperedAt) VerifyChain(IEnumerable<AuditLog> logs)
    {
        var ordered = logs.OrderBy(l => l.SequenceNum).ToList();
        byte[] expectedPrev = HashChainEntry.GenesisHash;

        foreach (var log in ordered)
        {
            // Recompute the current hash
            var parts = new[]
            {
                expectedPrev,
                Encoding.UTF8.GetBytes(DateTime.SpecifyKind(log.Timestamp, DateTimeKind.Utc).ToString("O")),
                Encoding.UTF8.GetBytes(log.Action.ToString()),
                Encoding.UTF8.GetBytes(log.EntityId ?? ""),
                Encoding.UTF8.GetBytes(log.UserId?.ToString() ?? "")
            };

            var combined = parts.SelectMany(p => p).ToArray();
            var recomputed = _hasher.Hash256(combined);

            if (!recomputed.SequenceEqual(log.CurrentHash))
                return (false, log.SequenceNum);

            if (!expectedPrev.SequenceEqual(log.PreviousHash))
                return (false, log.SequenceNum);

            expectedPrev = log.CurrentHash;
        }

        return (true, null);
    }
}

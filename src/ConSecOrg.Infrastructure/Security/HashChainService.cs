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
        var parts = new[]
        {
            prev,
            TimestampBytes(currentData.Timestamp),
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
        var ordered = logs.OrderBy(l => l.Timestamp).ToList();
        byte[] expectedPrev = HashChainEntry.GenesisHash;

        foreach (var log in ordered)
        {
            var parts = new[]
            {
                expectedPrev,
                TimestampBytes(log.Timestamp),
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

    // Truncate to milliseconds before encoding: datetime2 round-trip can lose sub-ms ticks,
    // so both write (AuditHelper) and verify must use the same precision ceiling.
    private static byte[] TimestampBytes(DateTime dt)
    {
        var utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        var ms = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, utc.Millisecond, DateTimeKind.Utc);
        return Encoding.UTF8.GetBytes(ms.ToString("O"));
    }
}

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
        var current = _hasher.Hash256(BuildInput(prev, currentData));
        return new HashChainEntry(prev, current);
    }

    public (bool Ok, long? TamperedAt) VerifyChain(IEnumerable<AuditLog> logs)
    {
        // Stable order: SequenceNum is IDENTITY — unique, monotonically increasing
        var ordered = logs.OrderBy(l => l.SequenceNum).ToList();
        if (ordered.Count == 0) return (true, null);

        byte[] expectedPrev = HashChainEntry.GenesisHash;
        long expectedSeq = ordered[0].SequenceNum;

        foreach (var log in ordered)
        {
            // Gap check: if any record was deleted, sequence numbers won't be contiguous
            if (log.SequenceNum != expectedSeq)
                return (false, log.SequenceNum);

            // Recompute hash from ALL fields — detect any field modification
            var recomputed = _hasher.Hash256(BuildInput(expectedPrev, log));

            if (!recomputed.SequenceEqual(log.CurrentHash))
                return (false, log.SequenceNum);

            if (!expectedPrev.SequenceEqual(log.PreviousHash))
                return (false, log.SequenceNum);

            expectedPrev = log.CurrentHash;
            expectedSeq++;
        }

        return (true, null);
    }

    private static byte[] BuildInput(byte[] previousHash, AuditLog log)
    {
        // Include ALL meaningful fields so tampering with ANY field is detected
        var parts = new[]
        {
            previousHash,
            TimestampBytes(log.Timestamp),
            Encoding.UTF8.GetBytes(log.Action.ToString()),
            Encoding.UTF8.GetBytes(log.EntityType ?? ""),
            Encoding.UTF8.GetBytes(log.EntityId ?? ""),
            Encoding.UTF8.GetBytes(log.UserId?.ToString() ?? ""),
            Encoding.UTF8.GetBytes(log.Status ?? ""),
            Encoding.UTF8.GetBytes(log.IpAddress ?? ""),
            Encoding.UTF8.GetBytes(log.DetailsJson ?? ""),
            BitConverter.GetBytes(log.SequenceNum)
        };
        return parts.SelectMany(p => p).ToArray();
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

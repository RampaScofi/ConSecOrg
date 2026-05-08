using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Security;
using FluentAssertions;

namespace ConSecOrg.Infrastructure.Tests.Security;

public class HashChainServiceTests
{
    private readonly HashChainService _sut = new();

    private static AuditLog BuildLog(long seqNum, AuditAction action = AuditAction.Login,
        string entityId = "ent1", Guid? userId = null,
        HashChainEntry? chain = null)
    {
        var entry = chain ?? new HashChainEntry(HashChainEntry.GenesisHash, new byte[32]);
        var log = new AuditLog(
            Guid.NewGuid(), userId, action, "TestEntity",
            entityId, "Success", "127.0.0.1", entry);
        // Set sequence number via reflection (EF identity column in prod)
        typeof(AuditLog).GetProperty(nameof(AuditLog.SequenceNum))!
            .SetValue(log, seqNum);
        return log;
    }

    [Fact]
    public void ComputeEntry_FirstEntry_UseGenesisHash()
    {
        var log = BuildLog(1);
        var result = _sut.ComputeEntry(null, log);

        result.PreviousHash.Should().BeEquivalentTo(HashChainEntry.GenesisHash, o => o.WithStrictOrdering());
        result.CurrentHash.Should().HaveCount(32);
        result.CurrentHash.Should().NotBeEquivalentTo(HashChainEntry.GenesisHash);
    }

    [Fact]
    public void ComputeEntry_SubsequentEntry_LinksToPreviousHash()
    {
        var log1 = BuildLog(1);
        var entry1 = _sut.ComputeEntry(null, log1);

        var log2 = BuildLog(2, AuditAction.Create);
        var entry2 = _sut.ComputeEntry(entry1.CurrentHash, log2);

        entry2.PreviousHash.Should().BeEquivalentTo(entry1.CurrentHash, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ComputeEntry_SameInputs_ProduceSameHash()
    {
        var log = BuildLog(1);
        // Force fixed timestamp for reproducibility
        var ts = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        typeof(AuditLog).GetProperty(nameof(AuditLog.Timestamp))!.SetValue(log, ts);

        var r1 = _sut.ComputeEntry(null, log);
        var r2 = _sut.ComputeEntry(null, log);

        r1.CurrentHash.Should().BeEquivalentTo(r2.CurrentHash, o => o.WithStrictOrdering());
    }

    [Fact]
    public void VerifyChain_ValidChain_ReturnsOk()
    {
        var logs = BuildValidChain(5);

        var (ok, tamperedAt) = _sut.VerifyChain(logs);

        ok.Should().BeTrue();
        tamperedAt.Should().BeNull();
    }

    [Fact]
    public void VerifyChain_EmptyChain_ReturnsOk()
    {
        var (ok, tamperedAt) = _sut.VerifyChain([]);

        ok.Should().BeTrue();
        tamperedAt.Should().BeNull();
    }

    [Fact]
    public void VerifyChain_TamperedCurrentHash_ReturnsFalseWithSeqNum()
    {
        var logs = BuildValidChain(5).ToList();

        // Tamper the CurrentHash of record 3
        var target = logs[2]; // seqNum = 3
        var tampered = target.CurrentHash.ToArray();
        tampered[0] ^= 0xFF;
        typeof(AuditLog).GetProperty(nameof(AuditLog.CurrentHash))!.SetValue(target, tampered);

        var (ok, tamperedAt) = _sut.VerifyChain(logs);

        ok.Should().BeFalse();
        tamperedAt.Should().Be(3);
    }

    [Fact]
    public void VerifyChain_TamperedPreviousHash_ReturnsFalseWithSeqNum()
    {
        var logs = BuildValidChain(5).ToList();

        // Tamper the PreviousHash of record 4 (breaks link from 3→4)
        var target = logs[3]; // seqNum = 4
        var tampered = target.PreviousHash.ToArray();
        tampered[0] ^= 0x01;
        typeof(AuditLog).GetProperty(nameof(AuditLog.PreviousHash))!.SetValue(target, tampered);

        var (ok, tamperedAt) = _sut.VerifyChain(logs);

        ok.Should().BeFalse();
        tamperedAt.Should().Be(4);
    }

    [Fact]
    public void VerifyChain_TamperedActionField_ReturnsFalse()
    {
        var logs = BuildValidChain(3).ToList();

        // Change the action on record 2 — hash will not match
        typeof(AuditLog).GetProperty(nameof(AuditLog.Action))!
            .SetValue(logs[1], AuditAction.Delete);

        var (ok, _) = _sut.VerifyChain(logs);

        ok.Should().BeFalse();
    }

    [Fact]
    public void VerifyChain_OutOfOrderLogs_StillVerifiesCorrectly()
    {
        var logs = BuildValidChain(4).ToList();
        // Shuffle
        logs.Reverse();

        var (ok, _) = _sut.VerifyChain(logs);

        ok.Should().BeTrue();
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private List<AuditLog> BuildValidChain(int count)
    {
        var logs = new List<AuditLog>();
        byte[]? prevHash = null;

        for (int i = 1; i <= count; i++)
        {
            var logData = BuildLog((long)i, AuditAction.Login, $"ent{i}");
            // Fix timestamp so ComputeEntry is deterministic
            var ts = new DateTime(2026, 1, i, 0, 0, 0, DateTimeKind.Utc);
            typeof(AuditLog).GetProperty(nameof(AuditLog.Timestamp))!.SetValue(logData, ts);

            var entry = _sut.ComputeEntry(prevHash, logData);

            // Write chain hashes back into the entity
            typeof(AuditLog).GetProperty(nameof(AuditLog.PreviousHash))!.SetValue(logData, entry.PreviousHash);
            typeof(AuditLog).GetProperty(nameof(AuditLog.CurrentHash))!.SetValue(logData, entry.CurrentHash);

            prevHash = entry.CurrentHash;
            logs.Add(logData);
        }

        return logs;
    }
}

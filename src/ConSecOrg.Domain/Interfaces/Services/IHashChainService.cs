using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Interfaces.Services;

public interface IHashChainService
{
    HashChainEntry ComputeEntry(byte[]? previousHash, AuditLog currentData);
    (bool Ok, long? TamperedAt) VerifyChain(IEnumerable<AuditLog> logs);
}

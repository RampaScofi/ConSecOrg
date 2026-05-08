namespace ConSecOrg.Application.Common.Interfaces;

public interface IEncryptionKeyStore
{
    void Store(Guid sessionId, byte[] keyMaterial);
    byte[]? Get(Guid sessionId);
    void Remove(Guid sessionId);
}

using System.Collections.Concurrent;
using ConSecOrg.Application.Common.Interfaces;

namespace ConSecOrg.Infrastructure.Security;

public sealed class EncryptionKeyStore : IEncryptionKeyStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _store = new();

    public void Store(Guid sessionId, byte[] keyMaterial) => _store[sessionId] = keyMaterial;

    public byte[]? Get(Guid sessionId) => _store.TryGetValue(sessionId, out var key) ? key : null;

    public void Remove(Guid sessionId)
    {
        if (_store.TryRemove(sessionId, out var key))
            Array.Clear(key, 0, key.Length);
    }
}

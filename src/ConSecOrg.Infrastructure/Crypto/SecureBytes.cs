using System.Runtime.InteropServices;

namespace ConSecOrg.Infrastructure.Crypto;

public sealed class SecureBytes : IDisposable
{
    private GCHandle _handle;
    private readonly byte[] _data;
    private bool _disposed;

    public int Length => _data.Length;

    public SecureBytes(int length)
    {
        _data = new byte[length];
        _handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
    }

    public SecureBytes(byte[] source)
    {
        _data = new byte[source.Length];
        Buffer.BlockCopy(source, 0, _data, 0, source.Length);
        _handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
    }

    public Span<byte> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _data.AsSpan();
    }

    public byte[] ToArray()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var copy = new byte[_data.Length];
        Buffer.BlockCopy(_data, 0, copy, 0, _data.Length);
        return copy;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Array.Clear(_data, 0, _data.Length);
        if (_handle.IsAllocated)
            _handle.Free();
    }
}

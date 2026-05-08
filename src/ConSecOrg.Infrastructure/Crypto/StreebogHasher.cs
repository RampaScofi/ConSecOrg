using Org.BouncyCastle.Crypto.Digests;

namespace ConSecOrg.Infrastructure.Crypto;

public sealed class StreebogHasher
{
    public byte[] Hash256(byte[] data)
    {
        var digest = new Gost3411_2012_256Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var result = new byte[32];
        digest.DoFinal(result, 0);
        return result;
    }

    public byte[] Hash512(byte[] data)
    {
        var digest = new Gost3411_2012_512Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var result = new byte[64];
        digest.DoFinal(result, 0);
        return result;
    }

    public byte[] Hash256(IEnumerable<byte[]> parts)
    {
        var digest = new Gost3411_2012_256Digest();
        foreach (var part in parts)
            digest.BlockUpdate(part, 0, part.Length);
        var result = new byte[32];
        digest.DoFinal(result, 0);
        return result;
    }
}

using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Interfaces.Services;

public interface ICryptoService
{
    EncryptedContent Encrypt(byte[] plaintext, byte[] encryptionKey);
    byte[] Decrypt(EncryptedContent content, byte[] encryptionKey);
    byte[] Hash256(byte[] data);
    byte[] Hash512(byte[] data);
    byte[] ComputeHmac(byte[] data, byte[] key);
    bool VerifyHmac(byte[] data, byte[] key, byte[] expectedHmac);
}

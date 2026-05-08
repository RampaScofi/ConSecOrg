namespace ConSecOrg.Domain.Interfaces.Services;

public interface IKdfService
{
    // Returns 64 bytes: [0..31] = encryption key verifier/key, [32..63] = HMAC key
    byte[] Derive(string password, byte[] salt, int iterations = 100_000);
    byte[] GenerateSalt();
}

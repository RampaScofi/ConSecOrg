namespace ConSecOrg.Application.Common.Interfaces;

public interface IPasswordHasher
{
    (byte[] Hash, byte[] Salt) Hash(string password);
    (bool IsValid, byte[] KeyMaterial) Verify(string password, byte[] hash, byte[] salt);
}

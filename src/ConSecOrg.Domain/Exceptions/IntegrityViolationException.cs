namespace ConSecOrg.Domain.Exceptions;

public class IntegrityViolationException : DomainException
{
    public IntegrityViolationException()
        : base("Data integrity check failed: HMAC mismatch. The content may have been tampered with.") { }

    public IntegrityViolationException(string entityType, string entityId)
        : base($"Integrity violation detected on {entityType} '{entityId}'. Content was not decrypted.") { }
}

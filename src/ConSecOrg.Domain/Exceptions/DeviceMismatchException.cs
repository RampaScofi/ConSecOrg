namespace ConSecOrg.Domain.Exceptions;

public class DeviceMismatchException : DomainException
{
    public DeviceMismatchException()
        : base("Device fingerprint mismatch. Access from unrecognized device is not allowed.") { }
}

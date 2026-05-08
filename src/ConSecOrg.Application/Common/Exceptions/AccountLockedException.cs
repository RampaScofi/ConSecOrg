namespace ConSecOrg.Application.Common.Exceptions;

public class AccountLockedException : Exception
{
    public AccountLockedException() : base("Account is locked due to too many failed login attempts.") { }
}

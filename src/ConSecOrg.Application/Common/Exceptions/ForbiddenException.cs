namespace ConSecOrg.Application.Common.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException() : base("Access denied.") { }
    public ForbiddenException(string message) : base(message) { }
}

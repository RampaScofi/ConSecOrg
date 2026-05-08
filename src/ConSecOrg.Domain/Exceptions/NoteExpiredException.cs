namespace ConSecOrg.Domain.Exceptions;

public class NoteExpiredException : DomainException
{
    public Guid NoteId { get; }

    public NoteExpiredException(Guid noteId)
        : base($"Note '{noteId}' has expired and been securely destroyed.")
    {
        NoteId = noteId;
    }
}

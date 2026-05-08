using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public enum ContactRequestStatus { Pending, Accepted, Declined }

public class ContactRequest : AuditableEntity<Guid>
{
    public Guid SenderId { get; private set; }
    public Guid ReceiverId { get; private set; }
    public ContactRequestStatus Status { get; private set; }

    public User? Sender { get; private set; }
    public User? Receiver { get; private set; }

    protected ContactRequest() { }

    public ContactRequest(Guid id, Guid senderId, Guid receiverId) : base(id)
    {
        SenderId = senderId;
        ReceiverId = receiverId;
        Status = ContactRequestStatus.Pending;
    }

    public void Accept() => Status = ContactRequestStatus.Accepted;
    public void Decline() => Status = ContactRequestStatus.Declined;
}

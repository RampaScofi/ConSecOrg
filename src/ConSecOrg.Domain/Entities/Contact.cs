using ConSecOrg.Domain.Common;
using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Entities;

public class Contact : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? LinkedUserId { get; private set; }
    public EncryptedContent Name { get; private set; } = null!;
    public EncryptedContent? Email { get; private set; }
    public EncryptedContent? Phone { get; private set; }
    public EncryptedContent? Notes { get; private set; }

    public User? User { get; private set; }

    protected Contact() { }

    public Contact(Guid id, Guid userId, EncryptedContent name,
        EncryptedContent? email = null,
        EncryptedContent? phone = null,
        EncryptedContent? notes = null,
        Guid? linkedUserId = null) : base(id)
    {
        UserId = userId;
        LinkedUserId = linkedUserId;
        Name = name;
        Email = email;
        Phone = phone;
        Notes = notes;
    }

    public void Update(EncryptedContent name, EncryptedContent? email, EncryptedContent? phone, EncryptedContent? notes)
    {
        Name = name;
        Email = email;
        Phone = phone;
        Notes = notes;
    }
}

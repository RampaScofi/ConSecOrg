using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Contacts;
using MediatR;

namespace ConSecOrg.Application.Features.Contacts.Queries;

public record GetContactsQuery : IRequest<IReadOnlyList<ContactDto>>;

public class GetContactsQueryHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<GetContactsQuery, IReadOnlyList<ContactDto>>
{
    public async Task<IReadOnlyList<ContactDto>> Handle(GetContactsQuery query, CancellationToken ct)
    {
        var contacts = await uow.Contacts.GetByUserAsync(currentUser.UserId, ct);
        var key = currentUser.GetEncryptionKey();
        return contacts.Select(c => Decrypt(c, key)).ToList();
    }

    private ContactDto Decrypt(Domain.Entities.Contact c, byte[] key)
    {
        var dec = (Domain.ValueObjects.EncryptedContent? ec) =>
        {
            if (ec is null) return null;
            try { return System.Text.Encoding.UTF8.GetString(crypto.Decrypt(ec, key)); }
            catch { return "[Ошибка]"; }
        };
        return new ContactDto
        {
            Id = c.Id,
            LinkedUserId = c.LinkedUserId,
            Name = dec(c.Name) ?? string.Empty,
            Email = dec(c.Email),
            Phone = dec(c.Phone),
            Notes = dec(c.Notes),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}

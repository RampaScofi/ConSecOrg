using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Contacts;
using MediatR;

namespace ConSecOrg.Application.Features.Contacts.Queries;

public record GetContactByIdQuery(Guid ContactId) : IRequest<ContactDto>;

public class GetContactByIdQueryHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<GetContactByIdQuery, ContactDto>
{
    public async Task<ContactDto> Handle(GetContactByIdQuery query, CancellationToken ct)
    {
        var c = await uow.Contacts.GetByIdAsync(query.ContactId, ct)
            ?? throw new NotFoundException("Contact", query.ContactId);

        if (c.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        var key = currentUser.GetEncryptionKey();
        var dec = (Domain.ValueObjects.EncryptedContent? ec) =>
        {
            if (ec is null) return null;
            try { return System.Text.Encoding.UTF8.GetString(crypto.Decrypt(ec, key)); }
            catch { return "[Ошибка]"; }
        };

        return new ContactDto
        {
            Id = c.Id,
            Name = dec(c.Name) ?? string.Empty,
            Email = dec(c.Email),
            Phone = dec(c.Phone),
            Notes = dec(c.Notes),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}

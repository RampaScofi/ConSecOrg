using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using FluentValidation;
using MediatR;

namespace ConSecOrg.Application.Features.Contacts.Commands;

public record CreateContactCommand(
    string Name,
    string? Email,
    string? Phone,
    string? Notes,
    Guid? LinkedUserId = null) : IRequest<Guid>;

public class CreateContactCommandValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null).MaximumLength(255);
    }
}

public class CreateContactCommandHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<CreateContactCommand, Guid>
{
    public async Task<Guid> Handle(CreateContactCommand cmd, CancellationToken ct)
    {
        var key = currentUser.GetEncryptionKey();
        var enc = (string s) => crypto.Encrypt(System.Text.Encoding.UTF8.GetBytes(s), key);

        var id = Guid.NewGuid();
        var contact = new Contact(id, currentUser.UserId,
            enc(cmd.Name),
            cmd.Email is not null ? enc(cmd.Email) : null,
            cmd.Phone is not null ? enc(cmd.Phone) : null,
            cmd.Notes is not null ? enc(cmd.Notes) : null,
            cmd.LinkedUserId);

        await uow.Contacts.AddAsync(contact, ct);
        await uow.SaveChangesAsync(ct);
        return id;
    }
}

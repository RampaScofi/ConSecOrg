using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace ConSecOrg.Application.Features.Auth.Commands;

public record RegisterCommand(
    string Username,
    string Email,
    string Password,
    Guid RoleId) : IRequest<Guid>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public class RegisterCommandHandler(IUnitOfWork uow, IPasswordHasher hasher)
    : IRequestHandler<RegisterCommand, Guid>
{
    public async Task<Guid> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        if (await uow.Users.ExistsAsync(cmd.Username, cmd.Email, ct))
            throw new Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("Username",
                "Username or email already in use.")]);

        var (hash, salt) = hasher.Hash(cmd.Password);
        var id = Guid.NewGuid();
        var user = new User(id, cmd.Username, cmd.Email, hash, salt, cmd.RoleId);
        var settings = new UserSettings(Guid.NewGuid(), id);

        await uow.Users.AddAsync(user, ct);
        await uow.Users.AddSettingsAsync(settings, ct);
        await uow.SaveChangesAsync(ct);

        return id;
    }
}

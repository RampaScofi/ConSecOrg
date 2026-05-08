using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace ConSecOrg.Application.Features.Auth.Commands;

public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public class ChangePasswordCommandHandler(IUnitOfWork uow, IPasswordHasher hasher)
    : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(cmd.UserId, ct)
            ?? throw new NotFoundException("User", cmd.UserId);

        var (isValid, _) = hasher.Verify(cmd.CurrentPassword, user.PasswordHash, user.Salt);
        if (!isValid)
            throw new UnauthorizedAccessException("Current password is incorrect.");

        var (newHash, newSalt) = hasher.Hash(cmd.NewPassword);
        user.ChangePassword(newHash, newSalt);
        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);
    }
}

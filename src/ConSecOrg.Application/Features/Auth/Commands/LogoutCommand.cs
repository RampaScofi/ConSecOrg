using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using MediatR;

namespace ConSecOrg.Application.Features.Auth.Commands;

public record LogoutCommand(Guid SessionId) : IRequest;

public class LogoutCommandHandler(IUnitOfWork uow, IEncryptionKeyStore keyStore)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand cmd, CancellationToken ct)
    {
        await uow.Users.DeleteSessionAsync(cmd.SessionId, ct);
        keyStore.Remove(cmd.SessionId);
        await uow.SaveChangesAsync(ct);
    }
}

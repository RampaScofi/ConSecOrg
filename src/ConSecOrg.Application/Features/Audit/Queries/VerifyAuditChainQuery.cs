using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Audit;
using MediatR;

namespace ConSecOrg.Application.Features.Audit.Queries;

public record VerifyAuditChainQuery : IRequest<AuditChainVerifyResultDto>;

public class VerifyAuditChainQueryHandler(
    IUnitOfWork uow,
    IHashChainService chainService,
    ICurrentUserContext currentUser) : IRequestHandler<VerifyAuditChainQuery, AuditChainVerifyResultDto>
{
    public async Task<AuditChainVerifyResultDto> Handle(VerifyAuditChainQuery query, CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Auditor)
            throw new ForbiddenException();

        var logs = await uow.AuditLogs.GetAllOrderedAsync(ct);
        var (ok, tamperedAt) = chainService.VerifyChain(logs);

        return new AuditChainVerifyResultDto
        {
            IsIntact = ok,
            TamperedAtSequence = tamperedAt,
            TotalRecordsChecked = logs.Count,
            Message = ok
                ? $"Цепочка целостна. Проверено записей: {logs.Count}."
                : $"Обнаружено вмешательство в запись #{tamperedAt}!",
            VerifiedAt = DateTime.UtcNow
        };
    }
}

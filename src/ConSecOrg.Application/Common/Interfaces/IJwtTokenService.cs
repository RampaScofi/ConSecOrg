using ConSecOrg.Domain.Entities;

namespace ConSecOrg.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, Guid sessionId);
    string GenerateRefreshToken();
    Guid? ValidateRefreshToken(string token);
}

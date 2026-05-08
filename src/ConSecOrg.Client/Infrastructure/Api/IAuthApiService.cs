using ConSecOrg.Shared.DTOs.Auth;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IAuthApiService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<LoginResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync();
    Task<UserInfoDto> GetMeAsync();
    Task RegisterAsync(RegisterRequestDto request);
    Task RegisterSelfAsync(RegisterRequestDto request);
    Task ChangePasswordAsync(ChangePasswordRequestDto request);
    Task ChangeEmailAsync(ChangeEmailRequestDto request);
}

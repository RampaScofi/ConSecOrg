using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using MediatR;

namespace ConSecOrg.Application.Features.Users.Commands;

public record UpdateUserSettingsCommand(
    Guid UserId,
    string Theme,
    string AccentColor,
    int FontSize,
    string? LayoutSettings) : IRequest;

public class UpdateUserSettingsCommandHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<UpdateUserSettingsCommand>
{
    public async Task Handle(UpdateUserSettingsCommand cmd, CancellationToken ct)
    {
        // Пользователи могут менять только свои настройки; Admin — любые
        if (cmd.UserId != currentUser.UserId && currentUser.Role != Domain.Enumerations.UserRole.Admin)
            throw new ForbiddenException();

        var user = await uow.Users.GetByIdAsync(cmd.UserId, ct)
            ?? throw new NotFoundException("User", cmd.UserId);

        if (user.Settings is null)
        {
            var settings = new UserSettings(Guid.NewGuid(), cmd.UserId);
            settings.UpdateTheme(cmd.Theme, cmd.AccentColor, cmd.FontSize, cmd.LayoutSettings);
            await uow.Users.AddSettingsAsync(settings, ct);
        }
        else
        {
            user.Settings.UpdateTheme(cmd.Theme, cmd.AccentColor, cmd.FontSize, cmd.LayoutSettings);
        }

        await uow.SaveChangesAsync(ct);
    }
}

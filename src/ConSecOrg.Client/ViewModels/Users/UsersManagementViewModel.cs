using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Users;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Users;

public partial class UserRow : ObservableObject
{
    public Guid Id { get; }
    public string Username { get; }
    public string Email { get; }
    [ObservableProperty] private UserRoleDto _role;
    [ObservableProperty] private bool _isLocked;
    public DateTime? LastLoginAt { get; }
    public DateTime CreatedAt { get; }

    public string StatusLabel => IsLocked ? "Заблокирован" : "Активен";
    public string StatusColor => IsLocked ? "#EF5350" : "#4CAF50";

    public string LastLoginDisplay => LastLoginAt.HasValue
        ? LastLoginAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
        : "Никогда";

    public UserRow(UserDto dto)
    {
        Id = dto.Id;
        Username = dto.Username;
        Email = dto.Email;
        _role = dto.Role;
        _isLocked = dto.IsLocked;
        LastLoginAt = dto.LastLoginAt;
        CreatedAt = dto.CreatedAt;
    }

    partial void OnIsLockedChanged(bool value) => OnPropertyChanged(nameof(StatusLabel));
}

public partial class UsersManagementViewModel(IUsersManagementApiService usersApi) : BasePageViewModel
{
    private List<UserRow> _allUsers = [];

    [ObservableProperty] private ObservableCollection<UserRow> _users = [];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusText;

    // Role options for ComboBox
    public static IReadOnlyList<RoleOption> Roles { get; } =
    [
        new("Admin",   UserRoleDto.Admin,   new Guid("11111111-1111-1111-1111-111111111111")),
        new("Manager", UserRoleDto.Manager, new Guid("22222222-2222-2222-2222-222222222222")),
        new("Auditor", UserRoleDto.Auditor, new Guid("33333333-3333-3333-3333-333333333333")),
        new("User",    UserRoleDto.User,    new Guid("44444444-4444-4444-4444-444444444444")),
    ];

    public override Task OnNavigatedToAsync() => LoadAsync();

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = null;
        try
        {
            var list = await usersApi.GetAllUsersAsync();
            _allUsers = list.Select(u => new UserRow(u)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось загрузить список пользователей: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText.Trim().ToLower();
        var filtered = string.IsNullOrEmpty(q)
            ? _allUsers
            : _allUsers.Where(u =>
                u.Username.ToLower().Contains(q) ||
                u.Email.ToLower().Contains(q));
        Users = new ObservableCollection<UserRow>(filtered);
    }

    [RelayCommand]
    private async Task AssignRoleAsync((UserRow User, RoleOption Role) args)
    {
        try
        {
            await usersApi.AssignRoleAsync(args.User.Id, args.Role.Id);
            args.User.Role = args.Role.RoleDto;
            StatusText = $"Роль {args.Role.Name} назначена пользователю {args.User.Username}";
        }
        catch { StatusText = "Ошибка при смене роли."; }
    }

    [RelayCommand]
    private async Task ToggleLockAsync(UserRow user)
    {
        try
        {
            if (user.IsLocked)
            {
                await usersApi.UnlockUserAsync(user.Id);
                user.IsLocked = false;
                StatusText = $"Пользователь {user.Username} разблокирован.";
            }
            else
            {
                await usersApi.LockUserAsync(user.Id);
                user.IsLocked = true;
                StatusText = $"Пользователь {user.Username} заблокирован.";
            }
        }
        catch { StatusText = "Ошибка при блокировке."; }
    }
}

public record RoleOption(string Name, UserRoleDto RoleDto, Guid Id);

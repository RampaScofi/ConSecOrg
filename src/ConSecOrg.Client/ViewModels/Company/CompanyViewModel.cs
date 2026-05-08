using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Projects;
using System.Collections.ObjectModel;
using System.Windows;

namespace ConSecOrg.Client.ViewModels.Company;

public partial class CompanyViewModel(
    ISharedProjectsApiService projectsService,
    SessionService session,
    ModeService modeService,
    NavigationService navigation) : BasePageViewModel
{
    [ObservableProperty] private ObservableCollection<SharedProjectDto> _projects = [];
    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private string _joinCode = string.Empty;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private bool _isJoining;

    public string ServerUrl => modeService.ServerUrl ?? "—";
    public string CurrentUsername => session.CurrentUser?.Username ?? "—";
    public string CurrentRole => session.CurrentUser?.Role.ToString() ?? "—";

    public override Task OnNavigatedToAsync() => LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = null;
        try
        {
            var list = await projectsService.GetMyProjectsAsync();
            Projects = new ObservableCollection<SharedProjectDto>(list);
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось загрузить проекты: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;
        IsCreating = true;
        StatusText = null;
        try
        {
            var project = await projectsService.CreateProjectAsync(
                new CreateSharedProjectDto { Name = NewProjectName.Trim() });
            Projects.Insert(0, project);
            NewProjectName = string.Empty;
            StatusText = $"Проект создан. Код приглашения: {project.InviteCode}";
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("connect", StringComparison.OrdinalIgnoreCase) || msg.Contains("refused"))
                StatusText = $"Нет соединения с сервером ({ServerUrl}).";
            else if (msg.Contains("401") || msg.Contains("403"))
                StatusText = "Ошибка авторизации — войдите снова.";
            else
                StatusText = $"Ошибка создания: {ExtractServerMessage(msg)}";
        }
        finally { IsCreating = false; }
    }

    [RelayCommand]
    private async Task JoinProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinCode)) return;
        IsJoining = true;
        StatusText = null;
        try
        {
            var project = await projectsService.JoinProjectAsync(
                new JoinSharedProjectDto { InviteCode = JoinCode.Trim().ToUpper() });
            Projects.Insert(0, project);
            JoinCode = string.Empty;
            StatusText = $"✓ Вы присоединились к проекту «{project.Name}»";
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("409") || msg.Contains("уже участник") || msg.Contains("Already"))
                StatusText = "Вы уже участник этого проекта.";
            else if (msg.Contains("404") || msg.Contains("не найден") || msg.Contains("Not Found"))
                StatusText = "Неверный код приглашения — проект не найден.";
            else if (msg.Contains("401") || msg.Contains("403") || msg.Contains("Unauthorized"))
                StatusText = "Ошибка авторизации — войдите снова.";
            else if (msg.Contains("connect") || msg.Contains("refused") || msg.Contains("Unable to connect"))
                StatusText = $"Нет соединения с сервером ({ServerUrl}).";
            else
                StatusText = $"Ошибка: {ExtractServerMessage(msg)}";
        }
        finally { IsJoining = false; }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(SharedProjectDto project)
    {
        if (MessageBox.Show($"Удалить проект «{project.Name}»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await projectsService.DeleteProjectAsync(project.Id);
            Projects.Remove(project);
            StatusText = "Проект удалён.";
        }
        catch { StatusText = "Ошибка при удалении."; }
    }

    [RelayCommand]
    private void CopyInviteCode(SharedProjectDto project)
    {
        Clipboard.SetText(project.InviteCode);
        StatusText = $"Код {project.InviteCode} скопирован в буфер обмена";
    }

    [RelayCommand]
    private void OpenProject(SharedProjectDto? project)
    {
        if (project is null) return;
        navigation.NavigateTo<SharedProjectBoardViewModel>(vm => _ = vm.OpenAsync(project));
    }

    // Extract "message" field from JSON error body if present
    private static string ExtractServerMessage(string raw)
    {
        try
        {
            // raw looks like: "API error 500: {"message":"SqlException: ..."}"
            var jsonStart = raw.IndexOf('{');
            if (jsonStart < 0) return raw;
            var json = raw[jsonStart..];
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var prop))
                return prop.GetString() ?? raw;
        }
        catch { }
        return raw;
    }
}

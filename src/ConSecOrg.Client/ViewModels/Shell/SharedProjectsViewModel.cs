using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Shared.DTOs.Projects;
using System.Collections.ObjectModel;
using System.Windows;

namespace ConSecOrg.Client.ViewModels.Shell;

public partial class SharedProjectsViewModel(ISharedProjectsApiService projectsService) : ObservableObject
{
    [ObservableProperty] private ObservableCollection<SharedProjectDto> _projects = [];
    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private string _joinCode = string.Empty;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _isLoading;

    public Action? CloseRequested { get; set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var list = await projectsService.GetMyProjectsAsync();
            Projects = new ObservableCollection<SharedProjectDto>(list);
        }
        catch { StatusText = "Не удалось загрузить проекты."; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;
        try
        {
            var project = await projectsService.CreateProjectAsync(new CreateSharedProjectDto { Name = NewProjectName.Trim() });
            Projects.Insert(0, project);
            NewProjectName = string.Empty;
            StatusText = $"Проект создан. Код приглашения: {project.InviteCode}";
        }
        catch { StatusText = "Ошибка при создании проекта."; }
    }

    [RelayCommand]
    private async Task JoinProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinCode)) return;
        try
        {
            var project = await projectsService.JoinProjectAsync(new JoinSharedProjectDto { InviteCode = JoinCode.Trim() });
            Projects.Insert(0, project);
            JoinCode = string.Empty;
            StatusText = $"Вы присоединились к проекту «{project.Name}»";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message.Contains("409") ? "Вы уже участник этого проекта."
                : ex.Message.Contains("404") ? "Неверный код приглашения."
                : "Ошибка при подключении.";
        }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(SharedProjectDto project)
    {
        var result = MessageBox.Show($"Удалить проект «{project.Name}»?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            await projectsService.DeleteProjectAsync(project.Id);
            Projects.Remove(project);
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
    private void Close() => CloseRequested?.Invoke();
}

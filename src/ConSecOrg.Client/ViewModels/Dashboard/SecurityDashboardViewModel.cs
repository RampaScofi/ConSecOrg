using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Dashboard;

namespace ConSecOrg.Client.ViewModels.Dashboard;

public partial class SecurityDashboardViewModel(IDashboardApiService dashboardService) : BasePageViewModel
{
    public override string Title => "Безопасность";

    [ObservableProperty] private SecurityDashboardDto? _dashboard;

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Dashboard = await dashboardService.GetSecurityDashboardAsync();
        });
    }
}

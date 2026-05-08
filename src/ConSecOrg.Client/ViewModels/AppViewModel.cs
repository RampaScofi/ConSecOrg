using CommunityToolkit.Mvvm.ComponentModel;
using ConSecOrg.Client.ViewModels.Shell;
using ConSecOrg.Client.ViewModels.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace ConSecOrg.Client.ViewModels;

public partial class AppViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private object? _currentView;

    public AppViewModel(IServiceProvider services) => _services = services;

    public void ShowModeSelection() => CurrentView = _services.GetRequiredService<ModeSelectionViewModel>();

    public void ShowLogin() => CurrentView = _services.GetRequiredService<LoginViewModel>();

    public void ShowPin() => CurrentView = _services.GetRequiredService<PinViewModel>();

    public void ShowRegister() => CurrentView = _services.GetRequiredService<RegisterViewModel>();

    public void ShowShell()
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.Initialize();
        CurrentView = shell;
    }
}

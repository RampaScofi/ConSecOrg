using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;

namespace ConSecOrg.Client.ViewModels.Startup;

public partial class ModeSelectionViewModel(
    ModeService modeService,
    AppViewModel appVm) : BasePageViewModel
{
    public override string Title => "Выбор режима";

    [ObservableProperty]
    private string _serverUrl = "http://localhost:5205";

    [RelayCommand]
    private void SelectPersonal()
    {
        modeService.SetPersonal();
        appVm.ShowPin();
    }

    [RelayCommand]
    private void SelectCorporate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
            ServerUrl = "http://localhost:5205";
        modeService.SetCorporate(ServerUrl);
        appVm.ShowLogin();
    }
}

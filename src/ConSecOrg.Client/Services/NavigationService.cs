using ConSecOrg.Client.ViewModels.Base;

namespace ConSecOrg.Client.Services;

public interface INavigationService
{
    BasePageViewModel? CurrentPage { get; }
    void NavigateTo<T>(Action<T>? configure = null) where T : BasePageViewModel;
    void GoBack();
    bool CanGoBack { get; }
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Stack<BasePageViewModel> _history = new();

    public BasePageViewModel? CurrentPage { get; private set; }
    public bool CanGoBack => _history.Count > 0;

    public event Action? NavigationChanged;

    public NavigationService(IServiceProvider services) => _services = services;

    public void NavigateTo<T>(Action<T>? configure = null) where T : BasePageViewModel
    {
        var vm = (T)_services.GetService(typeof(T))!;
        configure?.Invoke(vm);

        if (CurrentPage is not null)
            _history.Push(CurrentPage);

        CurrentPage = vm;
        NavigationChanged?.Invoke();

        _ = vm.OnNavigatedToAsync();
    }

    public void GoBack()
    {
        if (!CanGoBack) return;
        CurrentPage = _history.Pop();
        NavigationChanged?.Invoke();
        _ = CurrentPage.OnNavigatedToAsync();
    }

    public void Clear()
    {
        _history.Clear();
        CurrentPage = null;
    }
}

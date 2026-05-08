namespace ConSecOrg.Client.ViewModels.Base;

public abstract partial class BasePageViewModel : BaseViewModel
{
    public virtual string Title { get; } = string.Empty;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
    public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;
}

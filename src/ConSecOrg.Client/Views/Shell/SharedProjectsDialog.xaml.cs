using ConSecOrg.Client.ViewModels.Shell;
using System.Windows;

namespace ConSecOrg.Client.Views.Shell;

public partial class SharedProjectsDialog : Window
{
    public SharedProjectsDialog(SharedProjectsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested = () => Close();
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}

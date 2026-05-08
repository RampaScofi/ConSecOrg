using ConSecOrg.Client.ViewModels.Settings;
using System.Windows;

namespace ConSecOrg.Client.Views.Settings;

public partial class ChangeEmailDialog : Window
{
    public ChangeEmailDialog(ChangeEmailDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested = () => Close();
    }
}

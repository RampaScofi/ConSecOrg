using ConSecOrg.Client.ViewModels.Company;
using System.Windows;

namespace ConSecOrg.Client.Views.Company;

public partial class SharedTaskEditDialog : Window
{
    public SharedTaskEditDialog(SharedTaskEditViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested = Close;
    }
}

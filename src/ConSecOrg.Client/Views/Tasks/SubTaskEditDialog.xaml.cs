using ConSecOrg.Client.ViewModels.Tasks;
using System.Windows;

namespace ConSecOrg.Client.Views.Tasks;

public partial class SubTaskEditDialog : Window
{
    public SubTaskEditDialog(SubTaskEditViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested = () => Close();
    }
}

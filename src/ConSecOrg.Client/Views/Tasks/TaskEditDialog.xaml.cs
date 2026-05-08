using ConSecOrg.Client.ViewModels.Tasks;
using System.Windows;

namespace ConSecOrg.Client.Views.Tasks;

public partial class TaskEditDialog : Window
{
    public TaskEditDialog(TaskEditViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested = Close;
    }
}

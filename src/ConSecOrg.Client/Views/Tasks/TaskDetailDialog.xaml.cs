using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Tasks;

public partial class TaskDetailDialog : Window
{
    public TaskDetailDialog(TaskDetailViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested = () => Close();
    }

    private void SubDotsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>При уходе фокуса с inline-поля заголовка подзадачи — сохраняем изменения.</summary>
    private void SubTaskTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb
            && tb.DataContext is SubTaskItem sub
            && DataContext is TaskDetailViewModel vm)
        {
            vm.SaveSubTaskTitleCommand.Execute(sub);
        }
    }
}

using System.Windows;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Tasks;

public partial class MyTasksView : UserControl
{
    public MyTasksView()
    {
        InitializeComponent();
    }

    private void RowDotsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.DataContext = btn.Tag;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}

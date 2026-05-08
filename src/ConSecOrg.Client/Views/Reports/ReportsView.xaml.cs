using ConSecOrg.Client.ViewModels.Reports;
using System.Windows;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Reports;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReportsViewModel vm)
            _ = vm.LoadAsync();
    }
}

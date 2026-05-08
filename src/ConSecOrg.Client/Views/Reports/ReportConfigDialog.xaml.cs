using System.Windows;

namespace ConSecOrg.Client.Views.Reports;

public partial class ReportConfigDialog : Window
{
    public ReportConfigDialog()
    {
        InitializeComponent();
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

using System.Windows;

namespace ConSecOrg.Client.Views.Reports;

public partial class AddColumnDialog : Window
{
    public AddColumnDialog()
    {
        InitializeComponent();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

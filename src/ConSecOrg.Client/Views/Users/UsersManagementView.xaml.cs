using ConSecOrg.Client.ViewModels.Users;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Users;

public partial class UsersManagementView : UserControl
{
    public UsersManagementView()
    {
        InitializeComponent();
    }

    private void OnRoleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.Tag is not UserRow user) return;
        if (cb.SelectedItem is not RoleOption role) return;
        if (DataContext is UsersManagementViewModel vm)
            _ = vm.AssignRoleCommand.ExecuteAsync((user, role));
    }
}

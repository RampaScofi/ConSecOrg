using ConSecOrg.Client.ViewModels.Startup;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Startup;

public partial class LoginView : UserControl
{
    public LoginView() => InitializeComponent();

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = PasswordBox.Password;
    }
}

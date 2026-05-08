using ConSecOrg.Client.ViewModels.Startup;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Startup;

public partial class RegisterView : UserControl
{
    public RegisterView() => InitializeComponent();

    private void PwdBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm) vm.Password = PwdBox.Password;
    }

    private void ConfirmPwdBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm) vm.ConfirmPassword = ConfirmPwdBox.Password;
    }
}

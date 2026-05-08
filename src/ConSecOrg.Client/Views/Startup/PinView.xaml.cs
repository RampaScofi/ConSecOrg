using ConSecOrg.Client.ViewModels.Startup;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Startup;

public partial class PinView : UserControl
{
    public PinView() => InitializeComponent();

    private void PinBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PinViewModel vm)
            vm.Pin = PinBox.Password;
    }

    private void ConfirmPinBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PinViewModel vm)
            vm.ConfirmPin = ConfirmPinBox.Password;
    }
}

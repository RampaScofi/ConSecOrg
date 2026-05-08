using ConSecOrg.Client.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Settings;

public partial class ChangePasswordDialog : Window
{
    private readonly ChangePasswordDialogViewModel _vm;

    public ChangePasswordDialog(ChangePasswordDialogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.CloseRequested = () => Close();

        SaveButton.Click += async (_, _) =>
        {
            _vm.CurrentPassword = CurrentPasswordBox.Password;
            _vm.NewPassword = NewPasswordBox.Password;
            _vm.ConfirmPassword = ConfirmPasswordBox.Password;
            await _vm.SaveCommand.ExecuteAsync(null);
        };
    }
}

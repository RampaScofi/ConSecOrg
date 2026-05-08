using ConSecOrg.Client.ViewModels.Contacts;
using ConSecOrg.Shared.DTOs.Contacts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ConSecOrg.Client.Views.Contacts;

public partial class ContactsView : UserControl
{
    public ContactsView() => InitializeComponent();

    private void ContactItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is ContactDto contact
            && DataContext is ContactsViewModel vm)
            vm.SelectContactCommand.Execute(contact);
    }

    private void DotsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is ContextMenu menu)
        {
            menu.PlacementTarget = btn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }
}

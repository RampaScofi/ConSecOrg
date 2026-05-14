using ConSecOrg.Client.ViewModels.Chats;
using System.Windows;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Chats;

public partial class ChatsView : UserControl
{
    public ChatsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ChatsViewModel oldVm)
            oldVm.ScrollToBottomRequested -= ScrollToBottom;
        if (e.NewValue is ChatsViewModel newVm)
            newVm.ScrollToBottomRequested += ScrollToBottom;
    }

    private void ScrollToBottom()
    {
        if (MessagesScroll is ScrollViewer sv)
            sv.ScrollToBottom();
    }

    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is ContextMenu cm)
        {
            cm.DataContext = DataContext;
            cm.PlacementTarget = btn;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            cm.IsOpen = true;
        }
    }
}

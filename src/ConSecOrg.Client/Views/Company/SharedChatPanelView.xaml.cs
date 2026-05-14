using ConSecOrg.Client.ViewModels.Company;
using System.Windows;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Company;

public partial class SharedChatPanelView : UserControl
{
    public SharedChatPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SharedChatPanelViewModel oldVm) oldVm.ScrollToBottomRequested -= ScrollToBottom;
        if (e.NewValue is SharedChatPanelViewModel newVm) newVm.ScrollToBottomRequested += ScrollToBottom;
    }

    private void ScrollToBottom()
    {
        if (MessagesScroll is ScrollViewer sv) sv.ScrollToBottom();
    }

    private void EmojiBtn_Click(object sender, RoutedEventArgs e)
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

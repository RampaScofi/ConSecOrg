using ConSecOrg.Client.ViewModels.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ConSecOrg.Client.Views.Tasks;

public partial class ChatPanelView : UserControl
{
    public ChatPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ChatPanelViewModel? _vm;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.ScrollToBottomRequested -= ScrollToBottom;

        _vm = e.NewValue as ChatPanelViewModel;

        if (_vm is not null)
            _vm.ScrollToBottomRequested += ScrollToBottom;
    }

    private void ScrollToBottom()
    {
        // Dispatch after layout pass so the new items are rendered before we scroll
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            () => MessagesScroller.ScrollToEnd());
    }
}

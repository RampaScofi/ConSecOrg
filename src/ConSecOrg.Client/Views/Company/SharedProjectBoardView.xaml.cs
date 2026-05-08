using ConSecOrg.Client.Services;
using ConSecOrg.Client.Themes;
using ConSecOrg.Client.ViewModels.Company;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ConSecOrg.Client.Views.Company;

public partial class SharedProjectBoardView : UserControl
{
    private SharedTaskCardViewModel? _dragSourceTask;
    private Point _dragStartPoint;

    public SharedProjectBoardView() => InitializeComponent();

    // ── Task card drag ─────────────────────────────────────────────────────

    internal void TaskCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SharedTaskCardViewModel card)
        {
            _dragSourceTask = card;
            _dragStartPoint = e.GetPosition(null);
        }
    }

    internal void TaskCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceTask == null) return;
        var diff = e.GetPosition(null) - _dragStartPoint;
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var src = _dragSourceTask;
            _dragSourceTask = null;
            DragDrop.DoDragDrop((DependencyObject)sender, src, DragDropEffects.Move);
        }
    }

    internal void TaskCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(SharedTaskCardViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    internal void TaskCard_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SharedTaskCardViewModel)) is not SharedTaskCardViewModel srcTask) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not SharedTaskCardViewModel tgtTask) return;
        if (srcTask == tgtTask) { e.Handled = true; return; }
        if (DataContext is not SharedProjectBoardViewModel vm) return;

        var targetCol = FindColumnContaining(vm, tgtTask);
        if (targetCol is not null)
            _ = vm.MoveTaskByDragAsync(srcTask, targetCol);
        e.Handled = true;
    }

    // ── Column drag/drop ───────────────────────────────────────────────────

    internal void Column_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(SharedTaskCardViewModel)) && sender is Border b)
            AnimateBorder(b, GetAccentBrush(), 200);
    }

    internal void Column_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(SharedTaskCardViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    internal void Column_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b)
            AnimateBorder(b, (Brush)FindResource("BorderBrush"), 200);
    }

    internal void Column_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SharedColumnViewModel tgtCol) return;
        if (sender is Border border)
            AnimateBorder(border, (Brush)FindResource("BorderBrush"), 200);

        if (e.Data.GetData(typeof(SharedTaskCardViewModel)) is SharedTaskCardViewModel srcTask)
        {
            if (DataContext is SharedProjectBoardViewModel vm)
                _ = vm.MoveTaskByDragAsync(srcTask, tgtCol);
            e.Handled = true;
        }
    }

    internal void Column_MouseLeave(object sender, MouseEventArgs e) { }

    // ── ⋮ context menu ────────────────────────────────────────────────────

    internal void DotsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.DataContext = btn.Tag;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    /// <summary>⋮ у подзадачи: Tag = SharedTaskCardViewModel (через sharedCardProxy).</summary>
    internal void CardSubDots_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    /// <summary>Сохранение заголовка подзадачи при потере фокуса.</summary>
    internal void CardSubTaskTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not SubTaskItem sub) return;
        if (tb.TryFindResource("sharedCardProxy") is BindingProxy proxy
            && proxy.Data is SharedTaskCardViewModel card)
        {
            card.SaveSubTitleCommand.Execute(sub);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static SharedColumnViewModel? FindColumnContaining(SharedProjectBoardViewModel vm, SharedTaskCardViewModel task)
    {
        foreach (var c in vm.Columns)
            if (c.Tasks.Contains(task)) return c;
        return null;
    }

    private Brush GetAccentBrush() => (Brush)FindResource("AccentBrush");

    private static void AnimateBorder(Border b, Brush to, int durationMs)
    {
        b.BorderBrush = to;
        var anim = new DoubleAnimation(0.7, 1.0, new Duration(TimeSpan.FromMilliseconds(durationMs)));
        b.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}

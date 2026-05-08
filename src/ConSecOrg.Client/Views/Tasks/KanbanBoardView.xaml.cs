using ConSecOrg.Client.Services;
using ConSecOrg.Client.Themes;
using ConSecOrg.Client.ViewModels.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ConSecOrg.Client.Views.Tasks;

public partial class KanbanBoardView : UserControl
{
    private KanbanColumnViewModel? _dragSourceColumn;
    private TaskCardViewModel? _dragSourceTask;
    private FrameworkElement? _dragSourceTaskElement;
    private Point _dragStartPoint;

    public KanbanBoardView() => InitializeComponent();

    // ──────────────────── Column drag (header) ────────────────────

    internal void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is KanbanColumnViewModel col)
        {
            _dragSourceColumn = col;
            _dragStartPoint = e.GetPosition(null);
        }
    }

    internal void ColumnHeader_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceColumn == null) return;
        var diff = e.GetPosition(null) - _dragStartPoint;
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var src = _dragSourceColumn;
            _dragSourceColumn = null;
            DragDrop.DoDragDrop((DependencyObject)sender, src, DragDropEffects.Move);
        }
    }

    // ──────────────────── Task card drag ────────────────────

    internal void TaskCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskCardViewModel card)
        {
            _dragSourceTask = card;
            _dragSourceTaskElement = fe;
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
            var srcElement = _dragSourceTaskElement;
            _dragSourceTask = null;
            _dragSourceTaskElement = null;

            // Полупрозрачность во время перетаскивания
            if (srcElement != null) AnimateOpacity(srcElement, 1.0, 0.4, 100);

            try
            {
                DragDrop.DoDragDrop((DependencyObject)sender, src, DragDropEffects.Move);
            }
            finally
            {
                if (srcElement != null) AnimateOpacity(srcElement, srcElement.Opacity, 1.0, 200);
            }
        }
    }

    internal void TaskCard_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TaskCardViewModel)))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    internal void TaskCard_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TaskCardViewModel)) is not TaskCardViewModel srcTask) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not TaskCardViewModel tgtTask) return;
        if (srcTask == tgtTask) { e.Handled = true; return; }

        if (DataContext is not KanbanBoardViewModel vm) return;

        // Найти колонку целевой задачи
        var targetCol = FindColumnContaining(vm, tgtTask);
        if (targetCol == null) return;

        _ = vm.MoveTaskByDragAsync(srcTask, targetCol);
        e.Handled = true;
    }

    private static KanbanColumnViewModel? FindColumnContaining(KanbanBoardViewModel vm, TaskCardViewModel task)
    {
        foreach (var c in vm.Columns)
            if (c.Tasks.Contains(task)) return c;
        return null;
    }

    // ──────────────────── Column drop / hover ────────────────────

    internal void Column_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TaskCardViewModel)) ||
            e.Data.GetDataPresent(typeof(KanbanColumnViewModel)))
        {
            if (sender is Border b)
                AnimateBorder(b, b.BorderBrush, GetAccentBrush(), 200);
        }
    }

    internal void Column_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(KanbanColumnViewModel)) ||
            e.Data.GetDataPresent(typeof(TaskCardViewModel)))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    internal void Column_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b)
            AnimateBorder(b, b.BorderBrush, (Brush)FindResource("BorderBrush"), 200);
    }

    internal void Column_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not KanbanColumnViewModel tgtCol) return;
        if (DataContext is not KanbanBoardViewModel vm) return;

        // Восстановить рамку
        if (sender is Border border)
            AnimateBorder(border, border.BorderBrush, (Brush)FindResource("BorderBrush"), 200);

        // 1) Перетаскивание колонки
        if (e.Data.GetData(typeof(KanbanColumnViewModel)) is KanbanColumnViewModel srcCol)
        {
            if (srcCol == tgtCol) { e.Handled = true; return; }
            var oldIdx = vm.Columns.IndexOf(srcCol);
            var newIdx = vm.Columns.IndexOf(tgtCol);
            if (oldIdx >= 0 && newIdx >= 0)
            {
                vm.Columns.Move(oldIdx, newIdx);
                vm.PersistColumnOrderCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // 2) Перетаскивание задачи между колонками
        if (e.Data.GetData(typeof(TaskCardViewModel)) is TaskCardViewModel srcTask)
        {
            _ = vm.MoveTaskByDragAsync(srcTask, tgtCol);
            e.Handled = true;
        }
    }

    internal void Column_MouseLeave(object sender, MouseEventArgs e)
    {
        _dragSourceColumn = null;
    }

    // ──────────────────── Helpers ────────────────────

    private Brush GetAccentBrush()
        => (Brush)FindResource("AccentBrush");

    private static void AnimateOpacity(FrameworkElement el, double from, double to, int durationMs)
    {
        var anim = new DoubleAnimation(from, to, new Duration(TimeSpan.FromMilliseconds(durationMs)));
        el.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private static void AnimateBorder(Border b, Brush from, Brush to, int durationMs)
    {
        // Простая смена кисти с лёгким fade-эффектом через Opacity rim
        b.BorderBrush = to;
        var anim = new DoubleAnimation(0.7, 1.0, new Duration(TimeSpan.FromMilliseconds(durationMs)))
        {
            AutoReverse = false
        };
        b.BeginAnimation(UIElement.OpacityProperty, anim);
    }

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

    /// <summary>⋮ у подзадачи в карточке: Tag = TaskCardViewModel (через cardProxy).</summary>
    internal void CardSubDots_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    /// <summary>Сохранение заголовка подзадачи при потере фокуса (inline-редактирование в карточке).</summary>
    internal void CardSubTaskTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not SubTaskItem sub) return;

        // cardProxy объявлен в Border.Resources внутри карточки → поиск вверх по дереву
        if (tb.TryFindResource("cardProxy") is BindingProxy proxy
            && proxy.Data is TaskCardViewModel card)
        {
            card.SaveSubTitleCommand.Execute(sub);
        }
    }
}

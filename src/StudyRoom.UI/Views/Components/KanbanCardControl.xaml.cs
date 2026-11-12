using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI.Views.Components;

public partial class KanbanCardControl : UserControl
{
    private Point _dragStartPoint;
    private bool _isDragEligible;

    public KanbanCardControl()
    {
        InitializeComponent();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(source) != null)
        {
            _isDragEligible = false;
            return;
        }

        _isDragEligible = true;
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragEligible = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragEligible) return;

        if (e.LeftButton == MouseButtonState.Pressed && DataContext is KanbanCardItemViewModel card)
        {
            Point currentPoint = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPoint;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragEligible = false;
                var data = new DataObject("KanbanCard", card);
                DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

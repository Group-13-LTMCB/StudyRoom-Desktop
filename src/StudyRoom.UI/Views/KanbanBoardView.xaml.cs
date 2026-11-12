using System.Windows;
using System.Windows.Controls;
using StudyRoom.Core.Enums;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI.Views;

public partial class KanbanBoardView : UserControl
{
    public KanbanBoardView()
    {
        InitializeComponent();
    }

    private void OnColumnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("KanbanCard"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnColumnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("KanbanCard") &&
            sender is FrameworkElement element &&
            element.Tag is string tagStr &&
            int.TryParse(tagStr, out int colIndex) &&
            DataContext is KanbanBoardViewModel vm)
        {
            if (e.Data.GetData("KanbanCard") is KanbanCardItemViewModel card)
            {
                var targetColumn = (KanbanColumnType)colIndex;

                if (e.OriginalSource is DependencyObject dropSource)
                {
                    var targetControl = FindVisualParent<Components.KanbanCardControl>(dropSource);
                    if (targetControl?.DataContext is KanbanCardItemViewModel targetCard && targetCard.Id != card.Id)
                    {
                        Point dropPos = e.GetPosition(targetControl);
                        bool dropAfter = dropPos.Y > (targetControl.ActualHeight / 2.0);
                        _ = vm.MoveCardRelativeToAsync(card, targetColumn, targetCard, dropAfter);
                        return;
                    }
                }

                if (card.Column != targetColumn)
                {
                    _ = vm.MoveCardAsync(card, targetColumn);
                }
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

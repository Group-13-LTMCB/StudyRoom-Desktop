using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI.Views;

public partial class PomodoroRoomView : UserControl
{
    public PomodoroRoomView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown += OnWindowPreviewKeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown -= OnWindowPreviewKeyDown;
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is PomodoroRoomViewModel { IsZenMode: true } vm)
        {
            vm.ExitZenMode();
            e.Handled = true;
        }
    }
}

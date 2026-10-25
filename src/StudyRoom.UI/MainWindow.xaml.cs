using StudyRoom.UI.ViewModels;
using Wpf.Ui.Controls;

namespace StudyRoom.UI;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

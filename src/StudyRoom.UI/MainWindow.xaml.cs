using StudyRoom.UI.ViewModels;
using Wpf.Ui.Controls;

namespace StudyRoom.UI;

public partial class MainWindow : FluentWindow
{
    public MainWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

using StudyRoom.UI.ViewModels;
using Wpf.Ui.Controls;

namespace StudyRoom.UI.Views.Components;

public partial class JoinGroupDialog : FluentWindow
{
    public JoinGroupDialog(JoinGroupDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () => Dispatcher.Invoke(Close);
        Loaded += async (_, _) => await viewModel.LoadExploreGroupsAsync();
    }
}

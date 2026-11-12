using StudyRoom.UI.ViewModels;
using Wpf.Ui.Controls;

namespace StudyRoom.UI.Views.Components;

public partial class MfaSetupDialog : FluentWindow
{
    public MfaSetupViewModel ViewModel { get; }

    public MfaSetupDialog(MfaSetupViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}

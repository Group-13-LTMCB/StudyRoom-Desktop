using System.Windows;
using StudyRoom.UI.ViewModels;
using Wpf.Ui.Controls;

namespace StudyRoom.UI.Views.Components;

public partial class ChangePasswordDialog : FluentWindow
{
    private ChangePasswordViewModel? _viewModel;

    public ChangePasswordDialog(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += () => Close();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is ChangePasswordViewModel vm)
        {
            if (e.PropertyName == nameof(ChangePasswordViewModel.OldPassword) && string.IsNullOrEmpty(vm.OldPassword) && OldPasswordInput.Password != string.Empty)
            {
                OldPasswordInput.Password = string.Empty;
            }
            if (e.PropertyName == nameof(ChangePasswordViewModel.NewPassword) && string.IsNullOrEmpty(vm.NewPassword) && NewPasswordInput.Password != string.Empty)
            {
                NewPasswordInput.Password = string.Empty;
            }
            if (e.PropertyName == nameof(ChangePasswordViewModel.ConfirmPassword) && string.IsNullOrEmpty(vm.ConfirmPassword) && ConfirmPasswordInput.Password != string.Empty)
            {
                ConfirmPasswordInput.Password = string.Empty;
            }
        }
    }

    private void OldPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.OldPassword = OldPasswordInput.Password;
        }
    }

    private void NewPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.NewPassword = NewPasswordInput.Password;
        }
    }

    private void ConfirmPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ConfirmPassword = ConfirmPasswordInput.Password;
        }
    }
}

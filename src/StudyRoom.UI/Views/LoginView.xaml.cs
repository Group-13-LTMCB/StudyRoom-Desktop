using System.Windows;
using System.Windows.Controls;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is LoginViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.Password) &&
            sender is LoginViewModel vm &&
            string.IsNullOrEmpty(vm.Password))
        {
            if (PasswordInput.Password != string.Empty)
            {
                PasswordInput.Password = string.Empty;
            }
            if (RegisterPasswordInput.Password != string.Empty)
            {
                RegisterPasswordInput.Password = string.Empty;
            }
        }
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = PasswordInput.Password;
        }
    }

    private void RegisterPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = RegisterPasswordInput.Password;
        }
    }
}

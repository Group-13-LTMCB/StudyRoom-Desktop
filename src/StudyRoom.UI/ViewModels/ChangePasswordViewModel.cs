using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.UI.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private string? _accessToken;

    [ObservableProperty]
    private string _oldPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string? _successMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    public event Action? PasswordChanged;
    public event Action? RequestClose;

    public ChangePasswordViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    public void Initialize(string accessToken)
    {
        _accessToken = accessToken;
        OldPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        SuccessMessage = null;
        IsLoading = false;
    }

    [RelayCommand]
    public async Task ChangePasswordAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            ErrorMessage = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OldPassword))
        {
            ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Vui lòng nhập mật khẩu mới.";
            return;
        }

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Xác nhận mật khẩu mới không khớp.";
            return;
        }

        if (OldPassword == NewPassword)
        {
            ErrorMessage = "Mật khẩu mới không được trùng với mật khẩu hiện tại.";
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _authService.ChangePasswordAsync(_accessToken, OldPassword, NewPassword);
            if (result.IsSuccess)
            {
                SuccessMessage = "🎉 Đổi mật khẩu thành công!";
                OldPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
                PasswordChanged?.Invoke();
            }
            else
            {
                ErrorMessage = result.Error ?? "Đổi mật khẩu không thành công. Vui lòng kiểm tra lại mật khẩu hiện tại.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}

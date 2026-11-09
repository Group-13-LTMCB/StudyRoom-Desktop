using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.ViewModels;

public enum AuthMode
{
    SignIn,
    SignUp,
    ConfirmOtp,
    ForgotPassword,
    ResetPassword
}

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ISecureStorageService _secureStorageService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string? _successMessage;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _otpCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignInMode))]
    [NotifyPropertyChangedFor(nameof(IsSignUpMode))]
    [NotifyPropertyChangedFor(nameof(IsConfirmOtpMode))]
    [NotifyPropertyChangedFor(nameof(IsForgotPasswordMode))]
    [NotifyPropertyChangedFor(nameof(IsResetPasswordMode))]
    [NotifyPropertyChangedFor(nameof(IsRegisterMode))]
    private AuthMode _currentAuthMode = AuthMode.SignIn;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private AuthSession? _currentSession;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    public bool IsSignInMode => CurrentAuthMode == AuthMode.SignIn;
    public bool IsSignUpMode => CurrentAuthMode == AuthMode.SignUp;
    public bool IsConfirmOtpMode => CurrentAuthMode == AuthMode.ConfirmOtp;
    public bool IsForgotPasswordMode => CurrentAuthMode == AuthMode.ForgotPassword;
    public bool IsResetPasswordMode => CurrentAuthMode == AuthMode.ResetPassword;

    public bool IsRegisterMode
    {
        get => CurrentAuthMode == AuthMode.SignUp;
        set => CurrentAuthMode = value ? AuthMode.SignUp : AuthMode.SignIn;
    }

    public event Action<AuthSession>? LoginSucceeded;

    public LoginViewModel(IAuthService authService, ISecureStorageService secureStorageService)
    {
        _authService = authService;
        _secureStorageService = secureStorageService;
    }

    [RelayCommand]
    public async Task LoginAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Vui lòng nhập Email.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập Mật khẩu.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.LoginAsync(Email.Trim(), Password);
            if (result.IsSuccess && result.Value != null)
            {
                var tokens = result.Value;
                var session = new AuthSession
                {
                    UserId = tokens.UserId ?? Email.Trim(),
                    Email = Email.Trim(),
                    DisplayName = !string.IsNullOrWhiteSpace(tokens.DisplayName) ? tokens.DisplayName : Email.Trim().Split('@')[0],
                    IdToken = tokens.IdToken,
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                    ExpiresAtUtc = tokens.ExpiresAtUtc
                };

                CurrentSession = session;

                if (RememberMe)
                {
                    await _secureStorageService.SaveSessionAsync(session);
                }
                else
                {
                    await _secureStorageService.ClearSessionAsync();
                }

                SuccessMessage = "Đăng nhập thành công!";
                ErrorMessage = null;
                LoginSucceeded?.Invoke(session);
            }
            else
            {
                ErrorMessage = result.Error ?? "Đăng nhập không thành công. Vui lòng kiểm tra lại thông tin.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RegisterAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Vui lòng nhập Họ và tên.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Vui lòng nhập Email.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập Mật khẩu.";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.RegisterAsync(Email.Trim(), Password, DisplayName.Trim());
            if (result.IsSuccess)
            {
                SuccessMessage = "Đăng ký thành công! Vui lòng nhập mã OTP gửi về Email.";
                ErrorMessage = null;
                Password = string.Empty;
                CurrentAuthMode = AuthMode.ConfirmOtp;
            }
            else
            {
                ErrorMessage = result.Error ?? "Đăng ký không thành công.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmOtpAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Vui lòng nhập Email.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OtpCode))
        {
            ErrorMessage = "Vui lòng nhập mã OTP xác thực.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.ConfirmSignUpAsync(Email.Trim(), OtpCode.Trim());
            if (result.IsSuccess)
            {
                SuccessMessage = "Xác thực tài khoản thành công! Bạn có thể đăng nhập ngay.";
                ErrorMessage = null;
                OtpCode = string.Empty;
                CurrentAuthMode = AuthMode.SignIn;
            }
            else
            {
                ErrorMessage = result.Error ?? "Mã xác thực không hợp lệ hoặc đã hết hạn.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ResendOtpAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Vui lòng nhập Email để nhận lại mã.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.ResendConfirmationCodeAsync(Email.Trim());
            if (result.IsSuccess)
            {
                SuccessMessage = "Đã gửi lại mã OTP về Email của bạn.";
                ErrorMessage = null;
            }
            else
            {
                ErrorMessage = result.Error ?? "Không thể gửi lại mã OTP lúc này.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SendForgotPasswordCodeAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Vui lòng nhập Email để đặt lại mật khẩu.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.ForgotPasswordAsync(Email.Trim());
            if (result.IsSuccess)
            {
                SuccessMessage = "Đã gửi mã khôi phục về Email! Hãy nhập mã và mật khẩu mới.";
                ErrorMessage = null;
                CurrentAuthMode = AuthMode.ResetPassword;
            }
            else
            {
                ErrorMessage = result.Error ?? "Không thể gửi yêu cầu đặt lại mật khẩu.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmResetPasswordAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(OtpCode) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ Email, mã xác thực và mật khẩu mới.";
            return;
        }

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.ConfirmForgotPasswordAsync(Email.Trim(), OtpCode.Trim(), NewPassword);
            if (result.IsSuccess)
            {
                SuccessMessage = "Đổi mật khẩu thành công! Hãy đăng nhập bằng mật khẩu mới.";
                ErrorMessage = null;
                Password = NewPassword;
                NewPassword = string.Empty;
                OtpCode = string.Empty;
                CurrentAuthMode = AuthMode.SignIn;
            }
            else
            {
                ErrorMessage = result.Error ?? "Mã xác thực không đúng hoặc mật khẩu không hợp lệ.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ContinueAsGuestAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        var guestSession = new AuthSession
        {
            UserId = "guest_student_" + Guid.NewGuid().ToString("N")[..8],
            Email = "guest@studyroom.local",
            DisplayName = "Bạn (Khách)",
            IdToken = "guest_id_token",
            AccessToken = "guest_access_token",
            RefreshToken = "guest_refresh_token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        CurrentSession = guestSession;

        if (RememberMe)
        {
            await _secureStorageService.SaveSessionAsync(guestSession);
        }

        SuccessMessage = "Đăng nhập chế độ khách thành công!";
        LoginSucceeded?.Invoke(guestSession);
    }

    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    public void SwitchMode()
    {
        CurrentAuthMode = CurrentAuthMode == AuthMode.SignUp ? AuthMode.SignIn : AuthMode.SignUp;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    public void ShowLogin()
    {
        CurrentAuthMode = AuthMode.SignIn;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    public void ShowRegister()
    {
        CurrentAuthMode = AuthMode.SignUp;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    public void ShowForgotPassword()
    {
        CurrentAuthMode = AuthMode.ForgotPassword;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    public async Task<bool> CheckExistingSessionAsync()
    {
        try
        {
            var session = await _secureStorageService.LoadSessionAsync();
            if (session != null && session.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                CurrentSession = session;
                LoginSucceeded?.Invoke(session);
                return true;
            }
        }
        catch
        {
            // Ignore session load error
        }

        return false;
    }

    public void ResetForm()
    {
        Email = string.Empty;
        Password = string.Empty;
        NewPassword = string.Empty;
        DisplayName = string.Empty;
        OtpCode = string.Empty;
        ErrorMessage = null;
        SuccessMessage = null;
        IsBusy = false;
        CurrentAuthMode = AuthMode.SignIn;
    }
}

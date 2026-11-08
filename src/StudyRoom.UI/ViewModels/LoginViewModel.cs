using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.ViewModels;

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
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _isRegisterMode;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private AuthSession? _currentSession;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

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
                SuccessMessage = "Đăng ký thành công! Hãy đăng nhập để bắt đầu.";
                ErrorMessage = null;
                IsRegisterMode = false;
                Password = string.Empty;
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
    public void SwitchMode()
    {
        IsRegisterMode = !IsRegisterMode;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    public void ShowLogin()
    {
        if (IsRegisterMode)
        {
            IsRegisterMode = false;
            ErrorMessage = null;
            SuccessMessage = null;
        }
    }

    [RelayCommand]
    public void ShowRegister()
    {
        if (!IsRegisterMode)
        {
            IsRegisterMode = true;
            ErrorMessage = null;
            SuccessMessage = null;
        }
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
        ErrorMessage = null;
        SuccessMessage = null;
        IsBusy = false;
        IsRegisterMode = false;
    }
}

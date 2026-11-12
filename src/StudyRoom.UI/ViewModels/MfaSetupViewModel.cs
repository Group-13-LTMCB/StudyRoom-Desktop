using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Core.Interfaces;
using StudyRoom.UI.Helpers;

namespace StudyRoom.UI.ViewModels;

public partial class MfaSetupViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private string? _accessToken;
    private string? _userEmail;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isMfaEnabled;

    [ObservableProperty]
    private string _secretKey = string.Empty;

    [ObservableProperty]
    private BitmapImage? _qrCodeImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQrMode))]
    [NotifyPropertyChangedFor(nameof(IsSecretMode))]
    private int _selectedModeIndex = 0; // 0 = QR Code, 1 = Secret Key

    public bool IsQrMode => SelectedModeIndex == 0;
    public bool IsSecretMode => SelectedModeIndex == 1;

    [ObservableProperty]
    private string _verificationCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string? _successMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    public event Action<bool>? MfaStatusChanged;

    public MfaSetupViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task InitializeAsync(string accessToken, string userEmail, bool? isCurrentMfaEnabled = null)
    {
        _accessToken = accessToken;
        _userEmail = userEmail;
        ErrorMessage = null;
        SuccessMessage = null;
        VerificationCode = string.Empty;

        if (isCurrentMfaEnabled.HasValue)
        {
            IsMfaEnabled = isCurrentMfaEnabled.Value;
        }

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            ErrorMessage = "Vui lòng đăng nhập trước khi cấu hình MFA.";
            return;
        }

        IsLoading = true;
        try
        {
            if (!isCurrentMfaEnabled.HasValue)
            {
                var statusResult = await _authService.GetMfaStatusAsync(_accessToken);
                if (statusResult.IsSuccess)
                {
                    IsMfaEnabled = statusResult.Value;
                }
            }

            var result = await _authService.SetupTotpMfaAsync(_accessToken);
            if (result.IsSuccess && !string.IsNullOrEmpty(result.Value))
            {
                SecretKey = result.Value;
                string otpUri = $"otpauth://totp/StudyRoom:{_userEmail}?secret={SecretKey}&issuer=StudyRoom";
                QrCodeImage = QrCodeHelper.GenerateQrCodeBitmap(otpUri, 8);
            }
            else
            {
                ErrorMessage = result.Error ?? "Không thể lấy khóa xác thực TOTP.";
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
    public void SelectMode(string mode)
    {
        SelectedModeIndex = mode == "1" ? 1 : 0;
    }

    [RelayCommand]
    public void CopySecretKey()
    {
        if (!string.IsNullOrWhiteSpace(SecretKey))
        {
            Clipboard.SetText(SecretKey);
            SuccessMessage = "Đã sao chép khóa bí mật vào Clipboard!";
            ErrorMessage = null;
        }
    }

    [RelayCommand]
    public async Task VerifyAndEnableAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(VerificationCode))
        {
            ErrorMessage = "Vui lòng nhập mã 6 số từ Google Authenticator.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            ErrorMessage = "Phiên đăng nhập không hợp lệ.";
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _authService.VerifyTotpMfaAsync(_accessToken, VerificationCode.Trim());
            if (result.IsSuccess && result.Value)
            {
                IsMfaEnabled = true;
                SuccessMessage = "🎉 Kích hoạt bảo vệ 2 lớp (MFA) thành công!";
                VerificationCode = string.Empty;
                MfaStatusChanged?.Invoke(true);
            }
            else
            {
                ErrorMessage = result.Error ?? "Mã xác thực không hợp lệ. Vui lòng kiểm tra lại.";
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
    public async Task DisableMfaAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            ErrorMessage = "Phiên đăng nhập không hợp lệ.";
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _authService.SetMfaPreferenceAsync(_accessToken, false);
            if (result.IsSuccess)
            {
                IsMfaEnabled = false;
                SuccessMessage = "Đã tắt tính năng bảo mật 2 lớp.";
                MfaStatusChanged?.Invoke(false);
            }
            else
            {
                ErrorMessage = result.Error ?? "Không thể tắt tính năng MFA.";
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
}

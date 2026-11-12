using StudyRoom.Core.Common;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class ChangePasswordViewModelTests
{
    private class FakeAuthForPassword : IAuthService
    {
        public bool ShouldSucceed { get; set; } = true;
        public string? ErrorToReturn { get; set; }
        public string? LastOldPassword { get; private set; }
        public string? LastNewPassword { get; private set; }
        public string? LastAccessToken { get; private set; }

        public Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<LoginResult>.Failure("Not implemented"));

        public Task<Result<bool>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> ConfirmSignUpAsync(string email, string confirmationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> ResendConfirmationCodeAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> ConfirmForgotPasswordAsync(string email, string confirmationCode, string newPassword, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<AuthTokens>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AuthTokens>.Failure("Not implemented"));

        public Task<Result<AuthTokens>> RespondToMfaChallengeAsync(string email, string mfaCode, string mfaSession, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AuthTokens>.Failure("Not implemented"));

        public Task<Result<string>> SetupTotpMfaAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<string>.Success("SECRET"));

        public Task<Result<bool>> VerifyTotpMfaAsync(string accessToken, string totpCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> SetMfaPreferenceAsync(string accessToken, bool enabled, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> GetMfaStatusAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(false));

        public Task<Result<bool>> ChangePasswordAsync(string accessToken, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
        {
            LastAccessToken = accessToken;
            LastOldPassword = oldPassword;
            LastNewPassword = newPassword;

            if (!ShouldSucceed)
            {
                return Task.FromResult(Result<bool>.Failure(ErrorToReturn ?? "Mật khẩu hiện tại không chính xác."));
            }

            return Task.FromResult(Result<bool>.Success(true));
        }

        public Task LogoutAsync(string? accessToken = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private readonly FakeAuthForPassword _authService = new();
    private readonly ChangePasswordViewModel _vm;

    public ChangePasswordViewModelTests()
    {
        _vm = new ChangePasswordViewModel(_authService);
        _vm.Initialize("test_access_token_123");
    }

    [Fact]
    public void Initialize_ClearsFields()
    {
        _vm.OldPassword = "old";
        _vm.NewPassword = "new";
        _vm.ConfirmPassword = "new";
        _vm.Initialize("new_token");

        Assert.Empty(_vm.OldPassword);
        Assert.Empty(_vm.NewPassword);
        Assert.Empty(_vm.ConfirmPassword);
        Assert.False(_vm.HasError);
        Assert.False(_vm.HasSuccess);
        Assert.False(_vm.IsLoading);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithoutAccessToken_SetsError()
    {
        _vm.Initialize(string.Empty);
        _vm.OldPassword = "OldPassword123!";
        _vm.NewPassword = "NewPassword123!";
        _vm.ConfirmPassword = "NewPassword123!";

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("Phiên đăng nhập không hợp lệ", _vm.ErrorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_EmptyOldPassword_SetsError()
    {
        _vm.OldPassword = "";
        _vm.NewPassword = "NewPassword123!";
        _vm.ConfirmPassword = "NewPassword123!";

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("Vui lòng nhập mật khẩu hiện tại", _vm.ErrorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_EmptyNewPassword_SetsError()
    {
        _vm.OldPassword = "OldPassword123!";
        _vm.NewPassword = "";
        _vm.ConfirmPassword = "";

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("Vui lòng nhập mật khẩu mới", _vm.ErrorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShortNewPassword_SetsError()
    {
        _vm.OldPassword = "OldPassword123!";
        _vm.NewPassword = "123";
        _vm.ConfirmPassword = "123";

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("ít nhất 8 ký tự", _vm.ErrorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_PasswordMismatch_SetsError()
    {
        _vm.OldPassword = "OldPassword123!";
        _vm.NewPassword = "NewPassword123!";
        _vm.ConfirmPassword = "DifferentPass123!";

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("không khớp", _vm.ErrorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_SameOldAndNew_SetsError()
    {
        _vm.OldPassword = "SamePassword123!";
        _vm.NewPassword = "SamePassword123!";
        _vm.ConfirmPassword = "SamePassword123!";

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("không được trùng", _vm.ErrorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidInput_CallsServiceAndSucceeds()
    {
        _vm.OldPassword = "OldPassword123!";
        _vm.NewPassword = "NewPassword456!";
        _vm.ConfirmPassword = "NewPassword456!";

        bool eventFired = false;
        _vm.PasswordChanged += () => eventFired = true;

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasSuccess);
        Assert.False(_vm.HasError);
        Assert.True(eventFired);
        Assert.Equal("test_access_token_123", _authService.LastAccessToken);
        Assert.Equal("OldPassword123!", _authService.LastOldPassword);
        Assert.Equal("NewPassword456!", _authService.LastNewPassword);
        Assert.Empty(_vm.OldPassword);
        Assert.Empty(_vm.NewPassword);
        Assert.Empty(_vm.ConfirmPassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_ServiceFailure_SetsErrorMessage()
    {
        _authService.ShouldSucceed = false;
        _authService.ErrorToReturn = "Mật khẩu cũ không chính xác.";

        _vm.OldPassword = "WrongOldPassword!";
        _vm.NewPassword = "NewPassword456!";
        _vm.ConfirmPassword = "NewPassword456!";

        await _vm.ChangePasswordAsync();

        Assert.True(_vm.HasError);
        Assert.False(_vm.HasSuccess);
        Assert.Equal("Mật khẩu cũ không chính xác.", _vm.ErrorMessage);
    }

    [Fact]
    public void CloseCommand_FiresRequestClose()
    {
        bool closeFired = false;
        _vm.RequestClose += () => closeFired = true;

        _vm.Close();

        Assert.True(closeFired);
    }
}

using System.Threading;
using System.Windows;
using StudyRoom.Core.Common;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class MfaSetupViewModelTests
{
    private class FakeAuthForMfa : IAuthService
    {
        public bool ShouldSucceed { get; set; } = true;
        public string SecretToReturn { get; set; } = "TESTSECRETKEY123456";
        public bool CurrentMfaStatus { get; set; } = false;

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
            => Task.FromResult(ShouldSucceed ? Result<string>.Success(SecretToReturn) : Result<string>.Failure("Lỗi lấy Secret"));

        public Task<Result<bool>> VerifyTotpMfaAsync(string accessToken, string totpCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ShouldSucceed && totpCode == "123456" ? Result<bool>.Success(true) : Result<bool>.Failure("Mã không đúng"));

        public Task<Result<bool>> SetMfaPreferenceAsync(string accessToken, bool enabled, CancellationToken cancellationToken = default)
            => Task.FromResult(ShouldSucceed ? Result<bool>.Success(true) : Result<bool>.Failure("Lỗi bật tắt"));

        public Task<Result<bool>> GetMfaStatusAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(CurrentMfaStatus));

        public Task<Result<bool>> ChangePasswordAsync(string accessToken, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task LogoutAsync(string? accessToken = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void MfaSetupViewModel_SelectMode_SwitchesBetweenQrAndSecret()
    {
        var auth = new FakeAuthForMfa();
        var vm = new MfaSetupViewModel(auth);

        Assert.True(vm.IsQrMode);
        Assert.False(vm.IsSecretMode);

        vm.SelectMode("1");
        Assert.False(vm.IsQrMode);
        Assert.True(vm.IsSecretMode);

        vm.SelectMode("0");
        Assert.True(vm.IsQrMode);
        Assert.False(vm.IsSecretMode);
    }

    [Fact]
    public void MfaSetupViewModel_InitializeAsync_InStaThread_GeneratesQrAndSecret()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var auth = new FakeAuthForMfa();
                var vm = new MfaSetupViewModel(auth);

                vm.InitializeAsync("valid_token", "student@studyroom.io").GetAwaiter().GetResult();

                Assert.Equal("TESTSECRETKEY123456", vm.SecretKey);
                Assert.NotNull(vm.QrCodeImage);
                Assert.False(vm.HasError);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
    }

    [Fact]
    public async Task MfaSetupViewModel_VerifyAndEnableAsync_ValidCode_SetsMfaEnabled()
    {
        var auth = new FakeAuthForMfa();
        var vm = new MfaSetupViewModel(auth);

        // Initialize with token
        await vm.InitializeAsync("valid_token", "student@studyroom.io");

        vm.VerificationCode = "123456";
        await vm.VerifyAndEnableAsync();

        Assert.True(vm.IsMfaEnabled);
        Assert.True(vm.HasSuccess);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task MfaSetupViewModel_VerifyAndEnableAsync_InvalidCode_ShowsError()
    {
        var auth = new FakeAuthForMfa();
        var vm = new MfaSetupViewModel(auth);

        await vm.InitializeAsync("valid_token", "student@studyroom.io");

        vm.VerificationCode = "000000";
        await vm.VerifyAndEnableAsync();

        Assert.False(vm.IsMfaEnabled);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task MfaSetupViewModel_DisableMfaAsync_DisablesMfa()
    {
        var auth = new FakeAuthForMfa();
        var vm = new MfaSetupViewModel(auth);

        await vm.InitializeAsync("valid_token", "student@studyroom.io", isCurrentMfaEnabled: true);
        Assert.True(vm.IsMfaEnabled);

        await vm.DisableMfaAsync();

        Assert.False(vm.IsMfaEnabled);
        Assert.True(vm.HasSuccess);
    }

    [Fact]
    public async Task MfaSetupViewModel_InitializeAsync_AutoDetectsMfaStatus()
    {
        var auth = new FakeAuthForMfa { CurrentMfaStatus = true };
        var vm = new MfaSetupViewModel(auth);

        await vm.InitializeAsync("valid_token", "student@studyroom.io");

        Assert.True(vm.IsMfaEnabled);
    }

    [Fact]
    public async Task MfaSetupViewModel_MfaStatusChangedEvent_FiresOnEnableAndDisable()
    {
        var auth = new FakeAuthForMfa();
        var vm = new MfaSetupViewModel(auth);
        await vm.InitializeAsync("valid_token", "student@studyroom.io", isCurrentMfaEnabled: false);

        bool? notifiedStatus = null;
        vm.MfaStatusChanged += status => notifiedStatus = status;

        vm.VerificationCode = "123456";
        await vm.VerifyAndEnableAsync();

        Assert.True(notifiedStatus);

        await vm.DisableMfaAsync();

        Assert.False(notifiedStatus);
    }
}

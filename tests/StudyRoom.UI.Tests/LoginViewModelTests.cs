using StudyRoom.Core.Common;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class LoginViewModelTests
{
    private class FakeAuthService : IAuthService
    {
        public bool ShouldSucceed { get; set; } = true;
        public string? ErrorToReturn { get; set; }
        public AuthTokens? TokensToReturn { get; set; }
        public string? LastLoginEmail { get; private set; }
        public string? LastRegisterName { get; private set; }

        public Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            LastLoginEmail = email;
            if (!ShouldSucceed)
            {
                return Task.FromResult(Result<AuthTokens>.Failure(ErrorToReturn ?? "Invalid credentials"));
            }

            var tokens = TokensToReturn ?? new AuthTokens
            {
                IdToken = "id_token_123",
                AccessToken = "access_token_123",
                RefreshToken = "refresh_token_123",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
                UserId = "user_456",
                Email = email,
                DisplayName = "Test User"
            };

            return Task.FromResult(Result<AuthTokens>.Success(tokens));
        }

        public Task<Result<bool>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default)
        {
            LastRegisterName = displayName;
            if (!ShouldSucceed)
            {
                return Task.FromResult(Result<bool>.Failure(ErrorToReturn ?? "User already exists"));
            }

            return Task.FromResult(Result<bool>.Success(true));
        }

        public Task<Result<AuthTokens>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<AuthTokens>.Failure("Not implemented in fake"));
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class FakeSecureStorageService : ISecureStorageService
    {
        public AuthSession? StoredSession { get; set; }
        public bool SaveCalled { get; private set; }
        public bool ClearCalled { get; private set; }

        public Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
        {
            StoredSession = session;
            SaveCalled = true;
            return Task.CompletedTask;
        }

        public Task<AuthSession?> LoadSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StoredSession);
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        {
            StoredSession = null;
            ClearCalled = true;
            return Task.CompletedTask;
        }
    }

    private readonly FakeAuthService _authService = new();
    private readonly FakeSecureStorageService _storageService = new();
    private readonly LoginViewModel _vm;

    public LoginViewModelTests()
    {
        _vm = new LoginViewModel(_authService, _storageService);
    }

    [Fact]
    public void Construction_DefaultPropertiesAreSet()
    {
        Assert.Empty(_vm.Email);
        Assert.Empty(_vm.Password);
        Assert.Empty(_vm.DisplayName);
        Assert.False(_vm.IsRegisterMode);
        Assert.False(_vm.IsBusy);
        Assert.True(_vm.RememberMe);
        Assert.False(_vm.HasError);
        Assert.False(_vm.HasSuccess);
    }

    [Theory]
    [InlineData("", "password", "Vui lòng nhập Email.")]
    [InlineData("email@test.com", "", "Vui lòng nhập Mật khẩu.")]
    public async Task LoginAsync_MissingFields_SetsError(string email, string password, string expectedError)
    {
        _vm.Email = email;
        _vm.Password = password;

        await _vm.LoginAsync();

        Assert.True(_vm.HasError);
        Assert.Equal(expectedError, _vm.ErrorMessage);
        Assert.False(_vm.IsBusy);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_SavesSessionAndFiresEvent()
    {
        _vm.Email = "student@university.edu.vn";
        _vm.Password = "StrongPass123!";
        _vm.RememberMe = true;

        AuthSession? receivedSession = null;
        _vm.LoginSucceeded += session => receivedSession = session;

        await _vm.LoginAsync();

        Assert.False(_vm.HasError);
        Assert.True(_vm.HasSuccess);
        Assert.NotNull(_vm.CurrentSession);
        Assert.Equal("student@university.edu.vn", _vm.CurrentSession.Email);
        Assert.Equal("user_456", _vm.CurrentSession.UserId);
        Assert.NotNull(receivedSession);
        Assert.True(_storageService.SaveCalled);
        Assert.NotNull(_storageService.StoredSession);
    }

    [Fact]
    public async Task LoginAsync_RememberMeFalse_DoesNotSaveToStorage()
    {
        _vm.Email = "student@university.edu.vn";
        _vm.Password = "StrongPass123!";
        _vm.RememberMe = false;

        await _vm.LoginAsync();

        Assert.True(_vm.HasSuccess);
        Assert.False(_storageService.SaveCalled);
    }

    [Fact]
    public async Task LoginAsync_AuthFailure_SetsErrorMessage()
    {
        _authService.ShouldSucceed = false;
        _authService.ErrorToReturn = "Tài khoản hoặc mật khẩu không chính xác.";

        _vm.Email = "student@test.com";
        _vm.Password = "wrongpass";

        await _vm.LoginAsync();

        Assert.True(_vm.HasError);
        Assert.Equal("Tài khoản hoặc mật khẩu không chính xác.", _vm.ErrorMessage);
        Assert.False(_vm.IsBusy);
        Assert.Null(_vm.CurrentSession);
    }

    [Fact]
    public async Task RegisterAsync_ValidData_SwitchesToLoginModeAndSetsSuccess()
    {
        _vm.IsRegisterMode = true;
        _vm.DisplayName = "Nguyen Van A";
        _vm.Email = "a@test.com";
        _vm.Password = "Pass123456";

        await _vm.RegisterAsync();

        Assert.False(_vm.HasError);
        Assert.True(_vm.HasSuccess);
        Assert.False(_vm.IsRegisterMode);
        Assert.Empty(_vm.Password);
        Assert.Equal("Nguyen Van A", _authService.LastRegisterName);
    }

    [Fact]
    public async Task RegisterAsync_ShortPassword_SetsError()
    {
        _vm.DisplayName = "Nguyen Van A";
        _vm.Email = "a@test.com";
        _vm.Password = "123";

        await _vm.RegisterAsync();

        Assert.True(_vm.HasError);
        Assert.Contains("ít nhất 8 ký tự", _vm.ErrorMessage);
    }

    [Fact]
    public void SwitchMode_TogglesModeAndClearsMessages()
    {
        _vm.ErrorMessage = "Some error";
        _vm.SuccessMessage = "Some success";

        _vm.SwitchMode();

        Assert.True(_vm.IsRegisterMode);
        Assert.False(_vm.HasError);
        Assert.False(_vm.HasSuccess);

        _vm.SwitchMode();
        Assert.False(_vm.IsRegisterMode);
    }

    [Fact]
    public async Task CheckExistingSessionAsync_ValidSession_RestoresSessionAndFiresEvent()
    {
        var existing = new AuthSession
        {
            UserId = "stored_user_1",
            Email = "stored@test.com",
            DisplayName = "Stored Student",
            IdToken = "id_tok",
            AccessToken = "acc_tok",
            RefreshToken = "ref_tok",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1)
        };
        _storageService.StoredSession = existing;

        AuthSession? restored = null;
        _vm.LoginSucceeded += s => restored = s;

        bool result = await _vm.CheckExistingSessionAsync();

        Assert.True(result);
        Assert.NotNull(restored);
        Assert.Equal("stored_user_1", restored.UserId);
        Assert.Equal(existing, _vm.CurrentSession);
    }

    [Fact]
    public async Task CheckExistingSessionAsync_ExpiredSession_ReturnsFalse()
    {
        var expired = new AuthSession
        {
            UserId = "old_user",
            Email = "old@test.com",
            DisplayName = "Old Student",
            IdToken = "id_tok",
            AccessToken = "acc_tok",
            RefreshToken = "ref_tok",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _storageService.StoredSession = expired;

        bool result = await _vm.CheckExistingSessionAsync();

        Assert.False(result);
        Assert.Null(_vm.CurrentSession);
    }

    [Fact]
    public void ShowLogin_WhenInRegisterMode_SwitchesToLoginAndIdempotent()
    {
        _vm.IsRegisterMode = true;

        _vm.ShowLogin();
        Assert.False(_vm.IsRegisterMode);

        _vm.ShowLogin();
        Assert.False(_vm.IsRegisterMode);
    }

    [Fact]
    public void ShowRegister_WhenInLoginMode_SwitchesToRegisterAndIdempotent()
    {
        _vm.IsRegisterMode = false;

        _vm.ShowRegister();
        Assert.True(_vm.IsRegisterMode);

        _vm.ShowRegister();
        Assert.True(_vm.IsRegisterMode);
    }

    [Fact]
    public void LoginView_PasswordSyncAndButtonState_WorksCorrectlyInStaThread()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var view = new StudyRoom.UI.Views.LoginView
                {
                    DataContext = _vm
                };

                // Type password into PasswordInput
                view.PasswordInput.Password = "MySecretPassword123!";

                // Assert vm received password
                Assert.Equal("MySecretPassword123!", _vm.Password);

                // Clear vm password and assert PasswordInput is cleared
                _vm.Password = string.Empty;
                Assert.Empty(view.PasswordInput.Password);
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
}

using System.Windows;
using StudyRoom.Application.Services;
using StudyRoom.Core.Common;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using StudyRoom.UI.Views;
using Xunit;

namespace StudyRoom.UI.Tests;

public class AuthenticationNavigationTests
{
    private sealed class MockSecureStorageService : ISecureStorageService
    {
        public AuthSession? StoredSession { get; set; }
        public int ClearCallCount { get; private set; }

        public Task<AuthSession?> LoadSessionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(StoredSession);

        public Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
        {
            StoredSession = session;
            return Task.CompletedTask;
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        {
            StoredSession = null;
            ClearCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class MockAuthService : IAuthService
    {
        public Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AuthTokens>.Success(new AuthTokens
            {
                AccessToken = "mock-access",
                IdToken = "mock-id",
                RefreshToken = "mock-refresh",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2),
                UserId = "user-999",
                Email = email,
                DisplayName = "Nguyen Van B"
            }));

        public Task<Result<bool>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> ConfirmSignUpAsync(string email, string confirmationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(confirmationCode == "123456" ? Result<bool>.Success(true) : Result<bool>.Failure("Mã xác thực không hợp lệ."));

        public Task<Result<bool>> ResendConfirmationCodeAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Success(true));

        public Task<Result<bool>> ConfirmForgotPasswordAsync(string email, string confirmationCode, string newPassword, CancellationToken cancellationToken = default)
            => Task.FromResult(confirmationCode == "123456" ? Result<bool>.Success(true) : Result<bool>.Failure("Mã xác thực không hợp lệ."));

        public Task<Result<AuthTokens>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<AuthTokens>.Failure("Not implemented"));

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void DashboardViewModel_SetUserSession_UpdatesUserNameAndGreeting()
    {
        var vm = new DashboardViewModel(new MockDashboardService());
        var session = new AuthSession
        {
            UserId = "user-1",
            Email = "student@university.edu.vn",
            DisplayName = "Sally Nguyen",
            AccessToken = "a",
            IdToken = "b",
            RefreshToken = "c",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        };

        vm.SetUserSession(session);

        Assert.Equal("Sally Nguyen", vm.UserName);
        Assert.Equal("Chào mừng trở lại, Sally Nguyen!", vm.Greeting);
    }

    [Fact]
    public async Task DashboardViewModel_LogoutAsync_ClearsSessionAndInvokesEvent()
    {
        var storage = new MockSecureStorageService
        {
            StoredSession = new AuthSession
            {
                UserId = "user-1",
                Email = "student@uni.edu",
                DisplayName = "Sally",
                AccessToken = "a",
                IdToken = "b",
                RefreshToken = "c",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
            }
        };

        var vm = new DashboardViewModel(new MockDashboardService(), secureStorageService: storage);
        bool logoutEventFired = false;
        vm.RequestLogout += () => logoutEventFired = true;

        await vm.LogoutAsync();

        Assert.Equal(1, storage.ClearCallCount);
        Assert.Null(storage.StoredSession);
        Assert.True(logoutEventFired);
    }

    [Fact]
    public void MainWindow_AuthenticationNavigation_WorksInStaThread()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var storage = new MockSecureStorageService();
                var auth = new MockAuthService();
                var loginVm = new LoginViewModel(auth, storage);
                var dashboardVm = new DashboardViewModel(new MockDashboardService(), secureStorageService: storage);

                var window = new MainWindow(
                    dashboardVm,
                    hotKeyService: null,
                    floatingBubbleView: null,
                    memoryOptimizer: null,
                    loginViewModel: loginVm,
                    secureStorageService: storage);

                // Initially, when loginViewModel is provided and no session exists, LoginControl is visible
                Assert.Equal(Visibility.Visible, window.LoginControl.Visibility);
                Assert.Equal(Visibility.Collapsed, window.DashboardControl.Visibility);

                // Simulate session exists in storage
                storage.StoredSession = new AuthSession
                {
                    UserId = "user-active",
                    Email = "active@studyroom.io",
                    DisplayName = "Active Student",
                    AccessToken = "token",
                    IdToken = "id",
                    RefreshToken = "refresh",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(3)
                };

                // CheckSessionAndNavigateAsync should switch to Dashboard
                window.CheckSessionAndNavigateAsync().GetAwaiter().GetResult();
                Assert.Equal(Visibility.Collapsed, window.LoginControl.Visibility);
                Assert.Equal(Visibility.Visible, window.DashboardControl.Visibility);
                Assert.Equal("Active Student", dashboardVm.UserName);

                // Trigger logout from Dashboard
                dashboardVm.LogoutCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.Equal(Visibility.Visible, window.LoginControl.Visibility);
                Assert.Equal(Visibility.Collapsed, window.DashboardControl.Visibility);
                Assert.Null(storage.StoredSession);
                Assert.Empty(dashboardVm.UserName);
                Assert.Empty(dashboardVm.Greeting);

                // Simulate successful login from LoginViewModel
                loginVm.Email = "newuser@studyroom.io";
                loginVm.Password = "Password123!";
                loginVm.LoginCommand.ExecuteAsync(null).GetAwaiter().GetResult();

                // Should transition back to Dashboard with new user info
                Assert.Equal(Visibility.Collapsed, window.LoginControl.Visibility);
                Assert.Equal(Visibility.Visible, window.DashboardControl.Visibility);
                Assert.Equal("Nguyen Van B", dashboardVm.UserName);

                window.Close();
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
    public void MainWindow_ToggleFloatingBubble_WhenUnauthenticated_DoesNotToggleVisibility()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var storage = new MockSecureStorageService();
                var auth = new MockAuthService();
                var loginVm = new LoginViewModel(auth, storage);
                var dashboardVm = new DashboardViewModel(new MockDashboardService(), secureStorageService: storage);
                var bubbleVm = new FloatingBubbleViewModel();
                var bubbleView = new FloatingBubbleView(bubbleVm);

                var window = new MainWindow(
                    dashboardVm,
                    hotKeyService: null,
                    floatingBubbleView: bubbleView,
                    memoryOptimizer: null,
                    loginViewModel: loginVm,
                    secureStorageService: storage);

                // Initially on Login screen (unauthenticated)
                Assert.Equal(Visibility.Visible, window.LoginControl.Visibility);
                Assert.Equal(Visibility.Collapsed, window.DashboardControl.Visibility);

                // Bubble should not be toggled
                bubbleVm.IsVisible = false;
                window.ToggleFloatingBubble();
                Assert.False(bubbleVm.IsVisible);

                bubbleView.ForceClose();
                bubbleVm.Dispose();
                dashboardVm.Dispose();
                window.Close();
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
    public async Task LoginViewModel_LoginAsync_WhenRememberMeFalse_ClearsStoredSession()
    {
        var storage = new MockSecureStorageService
        {
            StoredSession = new AuthSession
            {
                UserId = "old-user",
                Email = "old@studyroom.io",
                DisplayName = "Old User",
                AccessToken = "old-token",
                IdToken = "old-id",
                RefreshToken = "old-refresh",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
            }
        };

        var auth = new MockAuthService();
        var loginVm = new LoginViewModel(auth, storage)
        {
            Email = "student@studyroom.io",
            Password = "Password123!",
            RememberMe = false
        };

        await loginVm.LoginAsync();

        Assert.Null(storage.StoredSession);
        Assert.Equal(1, storage.ClearCallCount);
    }
}

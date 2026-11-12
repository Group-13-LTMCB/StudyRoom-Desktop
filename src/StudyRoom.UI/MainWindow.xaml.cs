using System.Windows.Interop;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using StudyRoom.Infrastructure.OS;
using StudyRoom.UI.ViewModels;
using StudyRoom.UI.Views;
using Wpf.Ui.Controls;

namespace StudyRoom.UI;

public partial class MainWindow : FluentWindow
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly LoginViewModel? _loginViewModel;
    private readonly ISecureStorageService? _secureStorageService;
    private readonly IHotKeyService? _hotKeyService;
    private readonly FloatingBubbleView? _floatingBubbleView;
    private readonly IMemoryOptimizer? _memoryOptimizer;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;

    public const int ChatBubbleHotKeyId = Win32HotKeyService.ChatBubbleHotKeyId;

    public MainWindow(
        DashboardViewModel viewModel,
        IHotKeyService? hotKeyService = null,
        FloatingBubbleView? floatingBubbleView = null,
        IMemoryOptimizer? memoryOptimizer = null,
        LoginViewModel? loginViewModel = null,
        ISecureStorageService? secureStorageService = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _dashboardViewModel = viewModel;
        _hotKeyService = hotKeyService;
        _floatingBubbleView = floatingBubbleView;
        _memoryOptimizer = memoryOptimizer;
        _loginViewModel = loginViewModel;
        _secureStorageService = secureStorageService;

        DashboardControl.DataContext = _dashboardViewModel;
        if (_loginViewModel != null)
        {
            LoginControl.DataContext = _loginViewModel;
            _loginViewModel.LoginSucceeded += OnLoginSucceeded;
        }

        _dashboardViewModel.RequestLogout += OnLogoutRequested;
        _dashboardViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.SelectedNavIndex)
                || e.PropertyName == nameof(DashboardViewModel.IsZenMode))
            {
                UpdateFloatingBubbleVisibility();
            }
        };

        StateChanged += OnWindowStateChanged;
        Loaded += OnWindowLoaded;

        if (_loginViewModel != null && _secureStorageService != null)
        {
            LoginControl.Visibility = System.Windows.Visibility.Visible;
            DashboardControl.Visibility = System.Windows.Visibility.Collapsed;
        }
        else
        {
            LoginControl.Visibility = System.Windows.Visibility.Collapsed;
            DashboardControl.Visibility = System.Windows.Visibility.Visible;
        }
    }

    private async void OnWindowLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await CheckSessionAndNavigateAsync();
    }

    public async Task CheckSessionAndNavigateAsync()
    {
        if (_secureStorageService != null && _loginViewModel != null)
        {
            try
            {
                var session = await _secureStorageService.LoadSessionAsync();
                if (session != null && session.ExpiresAtUtc > DateTimeOffset.UtcNow)
                {
                    ShowDashboard(session);
                    return;
                }
            }
            catch
            {
                // Fallback to login on corrupted session
            }

            ShowLogin();
        }
        else
        {
            ShowDashboard();
        }
    }

    public void ShowLogin()
    {
        _floatingBubbleView?.Hide();
        _loginViewModel?.ResetForm();
        DashboardControl.Visibility = System.Windows.Visibility.Collapsed;
        LoginControl.Visibility = System.Windows.Visibility.Visible;
    }

    public void ShowDashboard(AuthSession? session = null)
    {
        if (session != null)
        {
            _dashboardViewModel.SetUserSession(session);
        }

        LoginControl.Visibility = System.Windows.Visibility.Collapsed;
        DashboardControl.Visibility = System.Windows.Visibility.Visible;

        UpdateFloatingBubbleVisibility();

        _ = _dashboardViewModel.LoadDataCommand.ExecuteAsync(null);
    }

    private void OnLoginSucceeded(AuthSession session)
    {
        ShowDashboard(session);
    }

    private void OnLogoutRequested()
    {
        ShowLogin();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == System.Windows.WindowState.Minimized)
        {
            _memoryOptimizer?.OptimizeMemory();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _windowHandle = new WindowInteropHelper(this).Handle;
        if (_windowHandle != IntPtr.Zero)
        {
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(HwndHook);

            if (_hotKeyService != null)
            {
                // Register Ctrl + Shift + C for Chat Bubble (with MOD_NOREPEAT to prevent repeat flicker)
                const uint vkC = 0x43;
                _hotKeyService.RegisterHotKey(
                    _windowHandle,
                    ChatBubbleHotKeyId,
                    Win32HotKeyService.MOD_CONTROL | Win32HotKeyService.MOD_SHIFT | Win32HotKeyService.MOD_NOREPEAT,
                    vkC);
            }
        }

        UpdateFloatingBubbleVisibility();
    }

    private void UpdateFloatingBubbleVisibility()
    {
        if (_floatingBubbleView == null) return;

        // Bong bóng chat chỉ được phép hiện khi tham gia Pomodoro (SelectedNavIndex == 1) và không ở chế độ Zen Mode
        if (DashboardControl.Visibility == System.Windows.Visibility.Visible 
            && _dashboardViewModel.SelectedNavIndex == 1 
            && !_dashboardViewModel.IsZenMode)
        {
            _floatingBubbleView.Show();
        }
        else
        {
            _floatingBubbleView.Hide();
        }
    }

    public void ToggleFloatingBubble()
    {
        if (DashboardControl.Visibility == System.Windows.Visibility.Visible)
        {
            _floatingBubbleView?.ViewModel.ToggleVisibility();
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32HotKeyService.WM_HOTKEY && wParam.ToInt32() == ChatBubbleHotKeyId)
        {
            ToggleFloatingBubble();
            handled = true;
        }

        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= OnWindowLoaded;
        StateChanged -= OnWindowStateChanged;

        if (_loginViewModel != null)
        {
            _loginViewModel.LoginSucceeded -= OnLoginSucceeded;
        }

        _dashboardViewModel.RequestLogout -= OnLogoutRequested;

        if (_windowHandle != IntPtr.Zero && _hotKeyService != null)
        {
            _hotKeyService.UnregisterHotKey(_windowHandle, ChatBubbleHotKeyId);
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }

        _floatingBubbleView?.ForceClose();

        if (DataContext is IDisposable disposableVm)
        {
            disposableVm.Dispose();
        }

        base.OnClosed(e);
    }
}

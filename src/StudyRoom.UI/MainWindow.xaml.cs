using System.Windows.Interop;
using StudyRoom.Core.Interfaces;
using StudyRoom.Infrastructure.OS;
using StudyRoom.UI.ViewModels;
using StudyRoom.UI.Views;
using Wpf.Ui.Controls;

namespace StudyRoom.UI;

public partial class MainWindow : FluentWindow
{
    private readonly IHotKeyService? _hotKeyService;
    private readonly FloatingBubbleView? _floatingBubbleView;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;

    public const int ChatBubbleHotKeyId = Win32HotKeyService.ChatBubbleHotKeyId;

    public MainWindow(
        DashboardViewModel viewModel,
        IHotKeyService? hotKeyService = null,
        FloatingBubbleView? floatingBubbleView = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _hotKeyService = hotKeyService;
        _floatingBubbleView = floatingBubbleView;
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

        _floatingBubbleView?.Show();
    }

    public void ToggleFloatingBubble()
    {
        _floatingBubbleView?.ViewModel.ToggleVisibility();
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

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.Infrastructure.OS;

[SupportedOSPlatform("windows")]
public sealed class Win32HotKeyService : IHotKeyService
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;

    public const int ChatBubbleHotKeyId = 9001;
    public const int PomodoroToggleHotKeyId = 9002;

    private readonly object _lock = new();
    private readonly HashSet<(IntPtr Handle, int Id)> _registeredKeys = [];
    private bool _disposed;

    [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    private static extern bool NativeRegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    private static extern bool NativeUnregisterHotKey(IntPtr hWnd, int id);

    public int RegisteredCount
    {
        get
        {
            lock (_lock)
            {
                return _registeredKeys.Count;
            }
        }
    }

    public bool IsRegistered(IntPtr windowHandle, int hotKeyId)
    {
        lock (_lock)
        {
            return _registeredKeys.Contains((windowHandle, hotKeyId));
        }
    }

    public bool RegisterHotKey(IntPtr windowHandle, int hotKeyId, uint modifiers, uint virtualKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        lock (_lock)
        {
            if (_registeredKeys.Contains((windowHandle, hotKeyId)))
            {
                NativeUnregisterHotKey(windowHandle, hotKeyId);
                _registeredKeys.Remove((windowHandle, hotKeyId));
            }

            bool result = NativeRegisterHotKey(windowHandle, hotKeyId, modifiers, virtualKey);
            if (result)
            {
                _registeredKeys.Add((windowHandle, hotKeyId));
            }

            return result;
        }
    }

    public bool UnregisterHotKey(IntPtr windowHandle, int hotKeyId)
    {
        if (_disposed || !OperatingSystem.IsWindows())
        {
            return false;
        }

        bool result = NativeUnregisterHotKey(windowHandle, hotKeyId);
        lock (_lock)
        {
            _registeredKeys.Remove((windowHandle, hotKeyId));
        }

        return result;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                foreach (var (handle, id) in _registeredKeys)
                {
                    NativeUnregisterHotKey(handle, id);
                }
            }

            _registeredKeys.Clear();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}

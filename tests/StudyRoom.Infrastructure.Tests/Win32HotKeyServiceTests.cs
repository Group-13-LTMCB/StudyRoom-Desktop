using System.Runtime.Versioning;
using StudyRoom.Infrastructure.OS;

namespace StudyRoom.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public class Win32HotKeyServiceTests
{
    [Fact]
    public void Construction_ShouldInitializeCleanly()
    {
        using var service = new Win32HotKeyService();
        Assert.Equal(0, service.RegisteredCount);
        Assert.False(service.IsRegistered(IntPtr.Zero, Win32HotKeyService.ChatBubbleHotKeyId));
    }

    [Fact]
    public void Constants_ShouldMatchWin32Specifications()
    {
        Assert.Equal(0x0001u, Win32HotKeyService.MOD_ALT);
        Assert.Equal(0x0002u, Win32HotKeyService.MOD_CONTROL);
        Assert.Equal(0x0004u, Win32HotKeyService.MOD_SHIFT);
        Assert.Equal(0x0008u, Win32HotKeyService.MOD_WIN);
        Assert.Equal(0x4000u, Win32HotKeyService.MOD_NOREPEAT);
        Assert.Equal(0x0312, Win32HotKeyService.WM_HOTKEY);
        Assert.Equal(9001, Win32HotKeyService.ChatBubbleHotKeyId);
        Assert.Equal(9002, Win32HotKeyService.PomodoroToggleHotKeyId);
    }

    [Fact]
    public void UnregisterHotKey_WhenNotRegistered_ShouldReturnFalseGracefully()
    {
        using var service = new Win32HotKeyService();
        bool result = service.UnregisterHotKey(new IntPtr(12345), Win32HotKeyService.ChatBubbleHotKeyId);
        Assert.False(result);
        Assert.Equal(0, service.RegisteredCount);
    }

    [Fact]
    public void RegisterHotKey_AfterDisposed_ShouldThrowObjectDisposedException()
    {
        var service = new Win32HotKeyService();
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            service.RegisterHotKey(IntPtr.Zero, Win32HotKeyService.ChatBubbleHotKeyId, Win32HotKeyService.MOD_CONTROL, 0x43));
    }

    [Fact]
    public void UnregisterHotKey_AfterDisposed_ShouldReturnFalse()
    {
        var service = new Win32HotKeyService();
        service.Dispose();

        bool result = service.UnregisterHotKey(IntPtr.Zero, Win32HotKeyService.ChatBubbleHotKeyId);
        Assert.False(result);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var service = new Win32HotKeyService();
        service.Dispose();
        service.Dispose(); // Should not throw
    }

    [Fact]
    public void RegisterHotKey_WithInvalidHandle_HandlesFailureGracefully()
    {
        using var service = new Win32HotKeyService();
        // Native RegisterHotKey with invalid hWnd returns false
        bool registered = service.RegisterHotKey(
            new IntPtr(-1),
            Win32HotKeyService.ChatBubbleHotKeyId,
            Win32HotKeyService.MOD_CONTROL,
            0x43);

        Assert.False(registered);
        Assert.Equal(0, service.RegisteredCount);
        Assert.False(service.IsRegistered(new IntPtr(-1), Win32HotKeyService.ChatBubbleHotKeyId));
    }
}

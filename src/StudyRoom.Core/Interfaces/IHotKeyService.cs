namespace StudyRoom.Core.Interfaces;

public interface IHotKeyService : IDisposable
{
    bool RegisterHotKey(IntPtr windowHandle, int hotKeyId, uint modifiers, uint virtualKey);
    bool UnregisterHotKey(IntPtr windowHandle, int hotKeyId);
}

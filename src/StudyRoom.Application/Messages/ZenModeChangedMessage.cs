namespace StudyRoom.Application.Messages;

/// <summary>
/// Phát khi trạng thái Zen Mode thay đổi trong PomodoroRoomView hoặc DashboardView.
/// </summary>
public sealed record ZenModeChangedMessage(bool IsZenMode);

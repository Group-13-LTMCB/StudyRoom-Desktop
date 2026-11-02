namespace StudyRoom.Application.Messages;

/// <summary>
/// Phát khi Host nhượng quyền điều khiển Pomodoro cho thành viên khác.
/// </summary>
public sealed record BatonPassedMessage(string NewHostUserId);

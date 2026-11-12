namespace StudyRoom.Application.Messages;

/// <summary>
/// Phát khi thành viên gia nhập hoặc rời phòng học nhóm.
/// </summary>
public sealed record PresenceChangedMessage(string UserId, string DisplayName, bool IsOnline);

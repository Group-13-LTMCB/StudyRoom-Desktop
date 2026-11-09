namespace StudyRoom.Application.Messages;

/// <summary>
/// Phát khi người dùng chuyển đổi nhóm học tập đang chọn trên thanh Rail.
/// </summary>
public sealed record GroupSwitchedMessage(Guid GroupId, string GroupName, bool IsPersonal, string JoinCode);

/// <summary>
/// Phát khi danh sách nhóm có thay đổi (tham gia nhóm mới, rời nhóm, giải tán).
/// </summary>
public sealed record GroupMembershipChangedMessage(Guid GroupId, string Action);

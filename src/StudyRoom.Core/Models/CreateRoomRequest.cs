using StudyRoom.Core.Enums;

namespace StudyRoom.Core.Models;

public sealed class CreateRoomRequest
{
    public string RoomName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public ControlMode ControlMode { get; set; } = ControlMode.HostOnly;
    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public int MaxMembers { get; set; } = 8;
}

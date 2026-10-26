namespace StudyRoom.Core.Models;

public sealed class StudyRoomSummary
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int MembersCount { get; set; }
    public int MaxMembers { get; set; }
    public bool IsActive { get; set; }
    public string HostName { get; set; } = string.Empty;
}

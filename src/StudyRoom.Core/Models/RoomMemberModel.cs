namespace StudyRoom.Core.Models;

public class RoomMemberModel
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarEmoji { get; set; } = "👤";
    public bool IsHost { get; set; }
    public bool HasBaton { get; set; }
    public string StatusText { get; set; } = "Đang tập trung 🔥";
    public bool IsFocusing { get; set; } = true;
}

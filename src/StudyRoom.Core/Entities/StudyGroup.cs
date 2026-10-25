namespace StudyRoom.Core.Entities;

public class StudyGroup
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public int MaxMembers { get; set; } = 10;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

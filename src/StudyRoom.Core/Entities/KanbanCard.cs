using StudyRoom.Core.Enums;

namespace StudyRoom.Core.Entities;

public class KanbanCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public KanbanColumnType Column { get; set; } = KanbanColumnType.Todo;
    public Guid? AssignedUserId { get; set; }
    public double Rank { get; set; } = 1.0;
    public int OrderIndex { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

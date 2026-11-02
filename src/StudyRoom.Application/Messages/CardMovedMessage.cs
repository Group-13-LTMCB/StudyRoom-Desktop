using StudyRoom.Core.Enums;

namespace StudyRoom.Application.Messages;

/// <summary>
/// Phát khi có thẻ Kanban đổi vị trí/cột.
/// </summary>
public sealed record CardMovedMessage(string CardId, KanbanColumnType NewColumn, double NewRank);

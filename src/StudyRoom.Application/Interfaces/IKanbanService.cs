using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;

namespace StudyRoom.Application.Interfaces;

public interface IKanbanService
{
    Task<IReadOnlyList<KanbanCard>> GetCardsAsync(string groupId, CancellationToken cancellationToken = default);
    Task<KanbanCard> AddCardAsync(string groupId, string title, string? description, KanbanColumnType column, CancellationToken cancellationToken = default);
    Task<KanbanCard> MoveCardAsync(Guid cardId, KanbanColumnType targetColumn, double? prevRank, double? nextRank, CancellationToken cancellationToken = default);
    Task DeleteCardAsync(Guid cardId, CancellationToken cancellationToken = default);
}

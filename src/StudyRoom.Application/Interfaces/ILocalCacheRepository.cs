using StudyRoom.Core.Entities;

namespace StudyRoom.Application.Interfaces;

public interface ILocalCacheRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CacheCardsAsync(string groupId, IEnumerable<KanbanCard> cards, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KanbanCard>> GetCachedCardsAsync(string groupId, CancellationToken cancellationToken = default);
}

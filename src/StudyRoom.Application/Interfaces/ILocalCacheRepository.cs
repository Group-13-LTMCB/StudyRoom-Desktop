using StudyRoom.Core.Entities;
using StudyRoom.Core.Models;

namespace StudyRoom.Application.Interfaces;

public interface ILocalCacheRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CacheCardsAsync(string groupId, IEnumerable<KanbanCard> cards, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KanbanCard>> GetCachedCardsAsync(string groupId, CancellationToken cancellationToken = default);
    Task CacheUserGroupsAsync(IEnumerable<GroupNavigationItem> groups, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroupNavigationItem>> GetCachedUserGroupsAsync(CancellationToken cancellationToken = default);
    Task RemoveCachedUserGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
}

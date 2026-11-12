using StudyRoom.Core.Common;
using StudyRoom.Core.Models;

namespace StudyRoom.Application.Interfaces;

public interface IGroupManagementService
{
    Task<IReadOnlyList<GroupNavigationItem>> GetUserGroupsAsync(CancellationToken cancellationToken = default);
    Task<Result<GroupNavigationItem>> JoinGroupByCodeAsync(string joinCode, CancellationToken cancellationToken = default);
    Task<Result<GroupNavigationItem>> CreateGroupAsync(string name, string subject, int maxMembers, CancellationToken cancellationToken = default);
    Task<Result> LeaveGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<Result> DisbandGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroupNavigationItem>> GetExploreGroupsAsync(CancellationToken cancellationToken = default);
    Task PinGroupAsync(Guid groupId, bool isPinned, CancellationToken cancellationToken = default);
    Task MuteGroupAsync(Guid groupId, bool isMuted, CancellationToken cancellationToken = default);
}

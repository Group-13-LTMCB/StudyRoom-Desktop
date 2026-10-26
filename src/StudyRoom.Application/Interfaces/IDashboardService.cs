using StudyRoom.Core.Common;
using StudyRoom.Core.Models;

namespace StudyRoom.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyActivityPoint>> GetWeeklyActivityAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudyRoomSummary>> GetRecentRoomsAsync(CancellationToken cancellationToken = default);
    Task<Result<StudyRoomSummary>> JoinRoomByCodeAsync(string roomCode, CancellationToken cancellationToken = default);
    Task<Result<StudyRoomSummary>> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default);
}

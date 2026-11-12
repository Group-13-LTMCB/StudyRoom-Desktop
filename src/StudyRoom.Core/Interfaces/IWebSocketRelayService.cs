using StudyRoom.Core.Enums;

namespace StudyRoom.Core.Interfaces;

public interface IWebSocketRelayService : IAsyncDisposable
{
    bool IsConnected { get; }
    string CurrentRoomId { get; }
    string UserId { get; }
    string DisplayName { get; }

    Task ConnectAsync(string accessToken, string userId, string displayName, string? initialRoomId = null, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SwitchRoomAsync(string newRoomId, CancellationToken cancellationToken = default);

    Task SendChatAsync(string content, CancellationToken cancellationToken = default);
    Task SendCardMovedAsync(string cardId, KanbanColumnType newColumn, double newRank, CancellationToken cancellationToken = default);
    Task SendStartPomodoroAsync(CancellationToken cancellationToken = default);
    Task SendPausePomodoroAsync(CancellationToken cancellationToken = default);
    Task SendPassBatonAsync(string newHostUserId, CancellationToken cancellationToken = default);
}

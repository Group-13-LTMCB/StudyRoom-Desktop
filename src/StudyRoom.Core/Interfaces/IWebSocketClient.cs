using StudyRoom.Core.Models;

namespace StudyRoom.Core.Interfaces;

public interface IWebSocketClient : IAsyncDisposable
{
    bool IsConnected { get; }
    event Func<WebSocketMessage, Task>? MessageReceived;
    event Func<Exception?, Task>? Disconnected;

    Task ConnectAsync(Uri serverUri, string accessToken, CancellationToken cancellationToken = default);
    Task SendAsync(WebSocketMessage message, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

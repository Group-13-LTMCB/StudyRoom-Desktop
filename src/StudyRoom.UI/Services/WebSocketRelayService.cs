using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using StudyRoom.Infrastructure.Configuration;

namespace StudyRoom.UI.Services;

public sealed class WebSocketRelayService : IWebSocketRelayService
{
    private readonly IWebSocketClient _client;
    private readonly IMessenger _messenger;
    private readonly AppConfiguration _configuration;
    private readonly IPomodoroEngine? _pomodoroEngine;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string _accessToken = string.Empty;
    private string _userId = string.Empty;
    private string _displayName = string.Empty;
    private string _currentRoomId = string.Empty;
    private bool _isHooked;
    private bool _disposed;

    public bool IsConnected => _client.IsConnected;
    public string CurrentRoomId => _currentRoomId;
    public string UserId => _userId;
    public string DisplayName => _displayName;

    public WebSocketRelayService(
        IWebSocketClient client,
        IMessenger messenger,
        AppConfiguration configuration,
        IPomodoroEngine? pomodoroEngine = null)
    {
        _client = client;
        _messenger = messenger;
        _configuration = configuration;
        _pomodoroEngine = pomodoroEngine;
    }

    public async Task ConnectAsync(string accessToken, string userId, string displayName, string? initialRoomId = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _accessToken = accessToken ?? string.Empty;
            _userId = userId ?? string.Empty;
            _displayName = displayName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(initialRoomId))
            {
                _currentRoomId = initialRoomId;
            }

            if (!_isHooked)
            {
                _client.MessageReceived += HandleRemoteMessageAsync;
                _isHooked = true;
            }

            if (!string.IsNullOrWhiteSpace(_configuration.WebSocketEndpointUrl) &&
                Uri.TryCreate(_configuration.WebSocketEndpointUrl, UriKind.Absolute, out var serverUri))
            {
                await _client.ConnectAsync(serverUri, _accessToken, cancellationToken);

                if (!string.IsNullOrWhiteSpace(_currentRoomId))
                {
                    await SendJoinRoomAsync(_currentRoomId, cancellationToken);
                }
            }
        }
        catch
        {
            // Fallback offline: don't crash UI on network failure
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_client.IsConnected && !string.IsNullOrWhiteSpace(_currentRoomId))
            {
                try
                {
                    var leaveMessage = WebSocketMessage.Create("LEAVE_ROOM", _currentRoomId, new
                    {
                        userId = _userId
                    });
                    await _client.SendAsync(leaveMessage, cancellationToken);
                }
                catch
                {
                    // Ignore network error on sending leave
                }
            }

            _currentRoomId = string.Empty;
            await _client.DisconnectAsync(cancellationToken);
        }
        catch
        {
            // Ignore disconnect errors
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SwitchRoomAsync(string newRoomId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _currentRoomId = newRoomId ?? string.Empty;
            if (_client.IsConnected && !string.IsNullOrWhiteSpace(_currentRoomId))
            {
                await SendJoinRoomAsync(_currentRoomId, cancellationToken);
            }
        }
        catch
        {
            // Fallback offline
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SendJoinRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        if (!_client.IsConnected || string.IsNullOrWhiteSpace(roomId)) return;

        var message = WebSocketMessage.Create("JOIN_ROOM", roomId, new
        {
            userId = _userId,
            displayName = _displayName
        });

        await _client.SendAsync(message, cancellationToken);
    }

    public async Task SendChatAsync(string content, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected || string.IsNullOrWhiteSpace(_currentRoomId) || string.IsNullOrWhiteSpace(content)) return;

        try
        {
            var message = WebSocketMessage.Create("SEND_CHAT", _currentRoomId, new
            {
                senderId = _userId,
                senderName = _displayName,
                content = content.Trim(),
                timestamp = DateTimeOffset.UtcNow.ToString("o"),
                messageId = Guid.NewGuid().ToString("N")
            });

            await _client.SendAsync(message, cancellationToken);
        }
        catch
        {
            // Offline fallback
        }
    }

    public async Task SendCardMovedAsync(string cardId, KanbanColumnType newColumn, double newRank, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected || string.IsNullOrWhiteSpace(_currentRoomId) || string.IsNullOrWhiteSpace(cardId)) return;

        try
        {
            var message = WebSocketMessage.Create("MOVE_KANBAN_CARD", _currentRoomId, new
            {
                cardId,
                newColumn = newColumn.ToString(),
                newRank
            });

            await _client.SendAsync(message, cancellationToken);
        }
        catch
        {
            // Offline fallback
        }
    }

    public async Task SendStartPomodoroAsync(CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected || string.IsNullOrWhiteSpace(_currentRoomId)) return;

        try
        {
            var message = WebSocketMessage.Create("START_POMODORO", _currentRoomId, new
            {
                requestedBy = _userId
            });

            await _client.SendAsync(message, cancellationToken);
        }
        catch
        {
            // Offline fallback
        }
    }

    public async Task SendPausePomodoroAsync(CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected || string.IsNullOrWhiteSpace(_currentRoomId)) return;

        try
        {
            var message = WebSocketMessage.Create("PAUSE_POMODORO", _currentRoomId, new
            {
                requestedBy = _userId
            });

            await _client.SendAsync(message, cancellationToken);
        }
        catch
        {
            // Offline fallback
        }
    }

    public async Task SendPassBatonAsync(string newHostUserId, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected || string.IsNullOrWhiteSpace(_currentRoomId) || string.IsNullOrWhiteSpace(newHostUserId)) return;

        try
        {
            var message = WebSocketMessage.Create("PASS_BATON", _currentRoomId, new
            {
                currentHostId = _userId,
                newHostUserId
            });

            await _client.SendAsync(message, cancellationToken);
        }
        catch
        {
            // Offline fallback
        }
    }

    public Task HandleRemoteMessageAsync(WebSocketMessage message)
    {
        if (message == null) return Task.CompletedTask;

        try
        {
            var action = message.Action?.ToUpperInvariant();
            switch (action)
            {
                case "CHAT_MESSAGE":
                    HandleRemoteChat(message);
                    break;
                case "CARD_MOVED":
                    HandleRemoteCardMoved(message);
                    break;
                case "POMODORO_STARTED":
                    HandleRemotePomodoroStarted(message);
                    break;
                case "POMODORO_PAUSED":
                    HandleRemotePomodoroPaused(message);
                    break;
                case "BATON_PASSED":
                    HandleRemoteBatonPassed(message);
                    break;
                case "MEMBER_JOINED":
                    HandleRemoteMemberJoined(message);
                    break;
            }
        }
        catch
        {
            // Ignore malformed remote payloads
        }

        return Task.CompletedTask;
    }

    private void HandleRemoteChat(WebSocketMessage message)
    {
        var data = message.Data;
        if (data.ValueKind != JsonValueKind.Object) return;

        var senderId = data.TryGetProperty("senderId", out var s) ? s.GetString() ?? string.Empty : string.Empty;
        var senderName = data.TryGetProperty("senderName", out var sn) ? sn.GetString() ?? "Member" : "Member";
        var content = data.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
        var msgId = data.TryGetProperty("messageId", out var m) ? m.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        if (data.TryGetProperty("timestamp", out var ts) && ts.GetString() is { } tsStr && DateTimeOffset.TryParse(tsStr, out var parsedTs))
        {
            timestamp = parsedTs;
        }

        var chat = new ChatMessage
        {
            MessageId = msgId,
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            Timestamp = timestamp,
            IsSelf = (senderId == _userId)
        };

        _messenger.Send(new ChatMessageReceivedMessage(chat));
    }

    private void HandleRemoteCardMoved(WebSocketMessage message)
    {
        var data = message.Data;
        if (data.ValueKind != JsonValueKind.Object) return;

        var cardId = data.TryGetProperty("cardId", out var c) ? c.GetString() ?? string.Empty : string.Empty;
        var colStr = data.TryGetProperty("newColumn", out var col) ? col.GetString() ?? "Todo" : "Todo";
        double rank = data.TryGetProperty("newRank", out var rk) && rk.TryGetDouble(out var rVal) ? rVal : 1.0;

        if (!Enum.TryParse<KanbanColumnType>(colStr, true, out var columnType))
        {
            columnType = KanbanColumnType.Todo;
        }

        _messenger.Send(new CardMovedMessage(cardId, columnType, rank));
    }

    private void HandleRemotePomodoroStarted(WebSocketMessage message)
    {
        var data = message.Data;
        if (data.ValueKind != JsonValueKind.Object) return;

        var phaseStr = data.TryGetProperty("phase", out var p) ? p.GetString() ?? "Focus" : "Focus";
        int durationSeconds = data.TryGetProperty("durationSeconds", out var d) && d.TryGetInt32(out var dVal) ? dVal : 1500;
        DateTimeOffset? targetEnd = null;
        if (data.TryGetProperty("targetEndTimeUtc", out var te) && te.GetString() is { } teStr && DateTimeOffset.TryParse(teStr, out var parsedTe))
        {
            targetEnd = parsedTe;
        }

        if (!Enum.TryParse<PomodoroPhase>(phaseStr, true, out var phase))
        {
            phase = PomodoroPhase.Focus;
        }

        _pomodoroEngine?.SyncFromServer(targetEnd, phase, TimerState.Running);
        _messenger.Send(new PhaseChangedMessage(phase, durationSeconds));
    }

    private void HandleRemotePomodoroPaused(WebSocketMessage message)
    {
        var data = message.Data;
        if (data.ValueKind != JsonValueKind.Object) return;

        var phaseStr = data.TryGetProperty("phase", out var p) ? p.GetString() ?? "Focus" : "Focus";
        if (!Enum.TryParse<PomodoroPhase>(phaseStr, true, out var phase))
        {
            phase = PomodoroPhase.Focus;
        }

        _pomodoroEngine?.SyncFromServer(null, phase, TimerState.Paused);
    }

    private void HandleRemoteBatonPassed(WebSocketMessage message)
    {
        var data = message.Data;
        if (data.ValueKind != JsonValueKind.Object) return;

        var newHost = data.TryGetProperty("newHostUserId", out var nh) ? nh.GetString() ?? string.Empty : string.Empty;
        _messenger.Send(new BatonPassedMessage(newHost));
    }

    private void HandleRemoteMemberJoined(WebSocketMessage message)
    {
        var data = message.Data;
        if (data.ValueKind != JsonValueKind.Object) return;

        var userId = data.TryGetProperty("userId", out var u) ? u.GetString() ?? string.Empty : string.Empty;
        var displayName = data.TryGetProperty("displayName", out var d) ? d.GetString() ?? "Member" : "Member";

        _messenger.Send(new PresenceChangedMessage(userId, displayName, true));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isHooked)
        {
            _client.MessageReceived -= HandleRemoteMessageAsync;
            _isHooked = false;
        }

        await DisconnectAsync();
        _lock.Dispose();
    }
}

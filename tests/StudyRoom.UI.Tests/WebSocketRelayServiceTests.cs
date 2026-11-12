using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Engines;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using StudyRoom.Infrastructure.Configuration;
using StudyRoom.UI.Services;
using Xunit;

namespace StudyRoom.UI.Tests;

public class WebSocketRelayServiceTests
{
    private sealed class FakeWebSocketClient : IWebSocketClient
    {
        public bool IsConnected { get; set; } = true;
        public Uri? ConnectedUri { get; private set; }
        public string? ConnectedAccessToken { get; private set; }
        public List<WebSocketMessage> SentMessages { get; } = [];

        public event Func<WebSocketMessage, Task>? MessageReceived;
        public event Func<Exception?, Task>? Disconnected;

        public Task ConnectAsync(Uri serverUri, string accessToken, CancellationToken cancellationToken = default)
        {
            ConnectedUri = serverUri;
            ConnectedAccessToken = accessToken;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(WebSocketMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public Task SimulateIncomingMessageAsync(WebSocketMessage message)
        {
            return MessageReceived != null ? MessageReceived.Invoke(message) : Task.CompletedTask;
        }

        public Task SimulateDisconnectedAsync(Exception? exception = null)
        {
            return Disconnected != null ? Disconnected.Invoke(exception) : Task.CompletedTask;
        }
    }

    private static (WebSocketRelayService Relay, FakeWebSocketClient Client, WeakReferenceMessenger Messenger, PomodoroEngine Engine) CreateSut()
    {
        var client = new FakeWebSocketClient();
        var messenger = new WeakReferenceMessenger();
        var engine = new PomodoroEngine();
        var config = new AppConfiguration
        {
            WebSocketEndpointUrl = "wss://test.execute-api.ap-southeast-1.amazonaws.com/prod"
        };

        var relay = new WebSocketRelayService(client, messenger, config, engine);
        return (relay, client, messenger, engine);
    }

    [Fact]
    public async Task ConnectAsync_EstablishesConnection_AndSendsJoinRoomIfSpecified()
    {
        var (relay, client, _, _) = CreateSut();

        await relay.ConnectAsync("test-token-123", "user-42", "Sally", "room-math");

        Assert.True(relay.IsConnected);
        Assert.Equal("user-42", relay.UserId);
        Assert.Equal("Sally", relay.DisplayName);
        Assert.Equal("room-math", relay.CurrentRoomId);
        Assert.Equal(new Uri("wss://test.execute-api.ap-southeast-1.amazonaws.com/prod"), client.ConnectedUri);
        Assert.Equal("test-token-123", client.ConnectedAccessToken);

        // Should have sent JOIN_ROOM message
        var joinMsg = Assert.Single(client.SentMessages);
        Assert.Equal("JOIN_ROOM", joinMsg.Action);
        Assert.Equal("room-math", joinMsg.RoomId);
    }

    [Fact]
    public async Task SwitchRoomAsync_UpdatesRoomId_AndSendsJoinRoomMessage()
    {
        var (relay, client, _, _) = CreateSut();
        await relay.ConnectAsync("token", "user-1", "User1", "room-1");
        client.SentMessages.Clear();

        await relay.SwitchRoomAsync("room-2");

        Assert.Equal("room-2", relay.CurrentRoomId);
        var switchMsg = Assert.Single(client.SentMessages);
        Assert.Equal("JOIN_ROOM", switchMsg.Action);
        Assert.Equal("room-2", switchMsg.RoomId);
    }

    [Fact]
    public async Task SendChatAsync_WhenConnected_SendsOutboundWebSocketMessage()
    {
        var (relay, client, _, _) = CreateSut();
        await relay.ConnectAsync("token", "user-alice", "Alice", "room-101");
        client.SentMessages.Clear();

        await relay.SendChatAsync("Xin chào phòng học!");

        var msg = Assert.Single(client.SentMessages);
        Assert.Equal("SEND_CHAT", msg.Action);
        Assert.Equal("room-101", msg.RoomId);

        var data = msg.Data;
        Assert.Equal("user-alice", data.GetProperty("senderId").GetString());
        Assert.Equal("Alice", data.GetProperty("senderName").GetString());
        Assert.Equal("Xin chào phòng học!", data.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendCardMovedAsync_WhenConnected_SendsMoveKanbanCardMessage()
    {
        var (relay, client, _, _) = CreateSut();
        await relay.ConnectAsync("token", "user-1", "Sally", "room-101");
        client.SentMessages.Clear();

        await relay.SendCardMovedAsync("card-99", KanbanColumnType.InProgress, 2.5);

        var msg = Assert.Single(client.SentMessages);
        Assert.Equal("MOVE_KANBAN_CARD", msg.Action);
        Assert.Equal("room-101", msg.RoomId);

        var data = msg.Data;
        Assert.Equal("card-99", data.GetProperty("cardId").GetString());
        Assert.Equal("InProgress", data.GetProperty("newColumn").GetString());
        Assert.Equal(2.5, data.GetProperty("newRank").GetDouble());
    }

    [Fact]
    public async Task SendStartPomodoroAsync_WhenConnected_SendsStartPomodoroMessage()
    {
        var (relay, client, _, _) = CreateSut();
        await relay.ConnectAsync("token", "user-1", "Sally", "room-101");
        client.SentMessages.Clear();

        await relay.SendStartPomodoroAsync();

        var msg = Assert.Single(client.SentMessages);
        Assert.Equal("START_POMODORO", msg.Action);
        Assert.Equal("room-101", msg.RoomId);
    }

    [Fact]
    public async Task SendPausePomodoroAsync_WhenConnected_SendsPausePomodoroMessage()
    {
        var (relay, client, _, _) = CreateSut();
        await relay.ConnectAsync("token", "user-1", "Sally", "room-101");
        client.SentMessages.Clear();

        await relay.SendPausePomodoroAsync();

        var msg = Assert.Single(client.SentMessages);
        Assert.Equal("PAUSE_POMODORO", msg.Action);
        Assert.Equal("room-101", msg.RoomId);
    }

    [Fact]
    public async Task SendPassBatonAsync_WhenConnected_SendsPassBatonMessage()
    {
        var (relay, client, _, _) = CreateSut();
        await relay.ConnectAsync("token", "user-1", "Sally", "room-101");
        client.SentMessages.Clear();

        await relay.SendPassBatonAsync("user-2");

        var msg = Assert.Single(client.SentMessages);
        Assert.Equal("PASS_BATON", msg.Action);
        Assert.Equal("room-101", msg.RoomId);

        var data = msg.Data;
        Assert.Equal("user-1", data.GetProperty("currentHostId").GetString());
        Assert.Equal("user-2", data.GetProperty("newHostUserId").GetString());
    }

    [Fact]
    public async Task HandleRemoteMessage_ChatMessage_PublishesChatMessageReceivedMessage()
    {
        var (relay, client, messenger, _) = CreateSut();
        await relay.ConnectAsync("token", "user-me", "Sally", "room-1");

        ChatMessageReceivedMessage? receivedMessage = null;
        messenger.Register<ChatMessageReceivedMessage>(this, (_, m) => receivedMessage = m);

        var inbound = WebSocketMessage.Create("CHAT_MESSAGE", "room-1", new
        {
            messageId = "msg-888",
            senderId = "user-bob",
            senderName = "Bob",
            content = "Chào mọi người!",
            timestamp = "2026-11-12T15:30:00Z"
        });

        await client.SimulateIncomingMessageAsync(inbound);

        Assert.NotNull(receivedMessage);
        Assert.Equal("msg-888", receivedMessage.Message.MessageId);
        Assert.Equal("user-bob", receivedMessage.Message.SenderId);
        Assert.Equal("Bob", receivedMessage.Message.SenderName);
        Assert.Equal("Chào mọi người!", receivedMessage.Message.Content);
        Assert.False(receivedMessage.Message.IsSelf);
    }

    [Fact]
    public async Task HandleRemoteMessage_CardMoved_PublishesCardMovedMessage()
    {
        var (relay, client, messenger, _) = CreateSut();
        await relay.ConnectAsync("token", "user-me", "Sally", "room-1");

        CardMovedMessage? receivedCardMove = null;
        messenger.Register<CardMovedMessage>(this, (_, m) => receivedCardMove = m);

        var inbound = WebSocketMessage.Create("CARD_MOVED", "room-1", new
        {
            cardId = "card-55",
            newColumn = "Done",
            newRank = 4.2
        });

        await client.SimulateIncomingMessageAsync(inbound);

        Assert.NotNull(receivedCardMove);
        Assert.Equal("card-55", receivedCardMove.CardId);
        Assert.Equal(KanbanColumnType.Done, receivedCardMove.NewColumn);
        Assert.Equal(4.2, receivedCardMove.NewRank);
    }

    [Fact]
    public async Task HandleRemoteMessage_PomodoroStarted_SyncsEngineAndPublishesPhaseChanged()
    {
        var (relay, client, messenger, engine) = CreateSut();
        await relay.ConnectAsync("token", "user-me", "Sally", "room-1");

        PhaseChangedMessage? phaseChanged = null;
        messenger.Register<PhaseChangedMessage>(this, (_, m) => phaseChanged = m);

        var targetEnd = DateTimeOffset.UtcNow.AddMinutes(25);
        var inbound = WebSocketMessage.Create("POMODORO_STARTED", "room-1", new
        {
            phase = "Focus",
            durationSeconds = 1500,
            targetEndTimeUtc = targetEnd.ToString("O")
        });

        await client.SimulateIncomingMessageAsync(inbound);

        Assert.NotNull(phaseChanged);
        Assert.Equal(PomodoroPhase.Focus, phaseChanged.NewPhase);
        Assert.Equal(1500, phaseChanged.DurationSeconds);
        Assert.Equal(TimerState.Running, engine.CurrentSession.State);
    }

    [Fact]
    public async Task HandleRemoteMessage_PomodoroPaused_SyncsEngineToPaused()
    {
        var (relay, client, _, engine) = CreateSut();
        await relay.ConnectAsync("token", "user-me", "Sally", "room-1");

        // Start engine first
        engine.Start();
        Assert.Equal(TimerState.Running, engine.CurrentSession.State);

        var inbound = WebSocketMessage.Create("POMODORO_PAUSED", "room-1", new
        {
            phase = "Focus"
        });

        await client.SimulateIncomingMessageAsync(inbound);

        Assert.Equal(TimerState.Paused, engine.CurrentSession.State);
    }

    [Fact]
    public async Task HandleRemoteMessage_BatonPassed_PublishesBatonPassedMessage()
    {
        var (relay, client, messenger, _) = CreateSut();
        await relay.ConnectAsync("token", "user-me", "Sally", "room-1");

        BatonPassedMessage? batonMessage = null;
        messenger.Register<BatonPassedMessage>(this, (_, m) => batonMessage = m);

        var inbound = WebSocketMessage.Create("BATON_PASSED", "room-1", new
        {
            newHostUserId = "user-david"
        });

        await client.SimulateIncomingMessageAsync(inbound);

        Assert.NotNull(batonMessage);
        Assert.Equal("user-david", batonMessage.NewHostUserId);
    }

    [Fact]
    public async Task HandleRemoteMessage_MemberJoined_PublishesPresenceChangedMessage()
    {
        var (relay, client, messenger, _) = CreateSut();
        await relay.ConnectAsync("token", "user-me", "Sally", "room-1");

        PresenceChangedMessage? presenceMessage = null;
        messenger.Register<PresenceChangedMessage>(this, (_, m) => presenceMessage = m);

        var inbound = WebSocketMessage.Create("MEMBER_JOINED", "room-1", new
        {
            userId = "user-alex",
            displayName = "Alex Nguyen"
        });

        await client.SimulateIncomingMessageAsync(inbound);

        Assert.NotNull(presenceMessage);
        Assert.Equal("user-alex", presenceMessage.UserId);
        Assert.Equal("Alex Nguyen", presenceMessage.DisplayName);
        Assert.True(presenceMessage.IsOnline);
    }

    [Fact]
    public async Task DisconnectAsync_SendsLeaveRoom_AndDisconnectsClient()
    {
        var (relay, client, _, _) = CreateSut();
        await relay.ConnectAsync("token", "user-1", "Sally", "room-101");
        client.SentMessages.Clear();

        await relay.DisconnectAsync();

        Assert.False(client.IsConnected);
        var leaveMsg = Assert.Single(client.SentMessages);
        Assert.Equal("LEAVE_ROOM", leaveMsg.Action);
        Assert.Equal("room-101", leaveMsg.RoomId);
    }
}

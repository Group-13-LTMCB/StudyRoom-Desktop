using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StudyRoom.Core.Models;
using StudyRoom.Infrastructure.Network;
using Xunit;

namespace StudyRoom.Infrastructure.Tests;

public class WebSocketClientTests
{
    private class FakeWebSocketAdapter : IWebSocketAdapter
    {
        private readonly System.Threading.Channels.Channel<(byte[] Chunk, bool EndOfMessage)> _channel = System.Threading.Channels.Channel.CreateUnbounded<(byte[] Chunk, bool EndOfMessage)>();

        public WebSocketState State { get; set; } = WebSocketState.None;
        public List<string> SentMessages { get; } = new();
        public Uri? ConnectedUri { get; private set; }
        public bool IsClosed { get; private set; }
        public bool FailOnSend { get; set; }

        public void EnqueueMessage(string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            _channel.Writer.TryWrite((bytes, true));
        }

        public void EnqueueChunkedMessage(string json, int chunkSize)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = Math.Min(chunkSize, bytes.Length - offset);
                byte[] chunk = new byte[count];
                Array.Copy(bytes, offset, chunk, 0, count);
                offset += count;
                bool isEnd = offset >= bytes.Length;
                _channel.Writer.TryWrite((chunk, isEnd));
            }
        }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            ConnectedUri = uri;
            State = WebSocketState.Open;
            return Task.CompletedTask;
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (FailOnSend)
            {
                throw new WebSocketException("Send failure on dead socket.");
            }

            string text = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            SentMessages.Add(text);
            return Task.CompletedTask;
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                if (_channel.Reader.TryRead(out var item))
                {
                    Array.Copy(item.Chunk, 0, buffer.Array!, buffer.Offset, item.Chunk.Length);
                    return new WebSocketReceiveResult(item.Chunk.Length, WebSocketMessageType.Text, item.EndOfMessage);
                }
            }

            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, "Closed");
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            State = WebSocketState.Closed;
            IsClosed = true;
            _channel.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public void SimulateNetworkDrop(Exception ex)
        {
            _channel.Writer.TryComplete(ex);
        }

        public void Dispose()
        {
            State = WebSocketState.Closed;
            _channel.Writer.TryComplete();
        }
    }

    [Fact]
    public void CalculateBackoffWithJitter_RespectsExponentialCurveAndJitterBounds()
    {
        var random = new Random(42);

        // Attempt 1: base = 1.0s, jitter ±20% -> [0.8, 1.2]
        var delay1 = WebSocketClient.CalculateBackoffWithJitter(1, initialSeconds: 1.0, maxSeconds: 30.0, jitterRatio: 0.2, random: random);
        Assert.InRange(delay1.TotalSeconds, 0.79, 1.21);

        // Attempt 2: base = 2.0s, jitter ±20% -> [1.6, 2.4]
        var delay2 = WebSocketClient.CalculateBackoffWithJitter(2, initialSeconds: 1.0, maxSeconds: 30.0, jitterRatio: 0.2, random: random);
        Assert.InRange(delay2.TotalSeconds, 1.59, 2.41);

        // Attempt 3: base = 4.0s, jitter ±20% -> [3.2, 4.8]
        var delay3 = WebSocketClient.CalculateBackoffWithJitter(3, initialSeconds: 1.0, maxSeconds: 30.0, jitterRatio: 0.2, random: random);
        Assert.InRange(delay3.TotalSeconds, 3.19, 4.81);

        // High attempt: capped at maxSeconds (30s) + jitter
        var delayHigh = WebSocketClient.CalculateBackoffWithJitter(10, initialSeconds: 1.0, maxSeconds: 30.0, jitterRatio: 0.2, random: random);
        Assert.InRange(delayHigh.TotalSeconds, 23.9, 36.1);
    }

    [Fact]
    public async Task ConnectAsync_EstablishesConnectionWithAccessTokenInQuery()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        var serverUri = new Uri("wss://api.studyroom.local/ws");
        await client.ConnectAsync(serverUri, "my-jwt-token");

        Assert.True(client.IsConnected);
        Assert.NotNull(adapter.ConnectedUri);
        Assert.Contains("token=my-jwt-token", adapter.ConnectedUri.Query);
    }

    [Fact]
    public async Task SendAsync_WhenConnected_SendsJsonMessage()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");

        var message = WebSocketMessage.Create("JOIN_ROOM", "room_123", new { userId = "u1" });
        await client.SendAsync(message);

        Assert.Single(adapter.SentMessages);
        var sent = JsonSerializer.Deserialize<WebSocketMessage>(adapter.SentMessages[0]);
        Assert.NotNull(sent);
        Assert.Equal("JOIN_ROOM", sent.Action);
        Assert.Equal("room_123", sent.RoomId);
    }

    [Fact]
    public async Task SendAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        var message = WebSocketMessage.Create("TEST", "room_1", new { });
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(message));
    }

    [Fact]
    public async Task ReceiveLoop_DispatchesIncomingMessage()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        WebSocketMessage? received = null;
        var tcs = new TaskCompletionSource<bool>();

        client.MessageReceived += msg =>
        {
            received = msg;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");

        var testMsg = WebSocketMessage.Create("CHAT_MESSAGE", "room_01", new { text = "Hello World" });
        adapter.EnqueueMessage(JsonSerializer.Serialize(testMsg));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);
        Assert.NotNull(received);
        Assert.Equal("CHAT_MESSAGE", received.Action);
        Assert.Equal("room_01", received.RoomId);
    }

    [Fact]
    public async Task ReceiveLoop_WhenPingReceived_RepliesWithPong()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");

        var ping = WebSocketMessage.Create("PING", "room_abc", new { ts = 1234 });
        adapter.EnqueueMessage(JsonSerializer.Serialize(ping));

        // Wait a brief moment for the receive loop to process and send pong
        for (int i = 0; i < 20 && adapter.SentMessages.Count == 0; i++)
        {
            await Task.Delay(50);
        }

        Assert.NotEmpty(adapter.SentMessages);
        var pong = JsonSerializer.Deserialize<WebSocketMessage>(adapter.SentMessages[0]);
        Assert.NotNull(pong);
        Assert.Equal("PONG", pong.Action);
        Assert.Equal("room_abc", pong.RoomId);
    }

    [Fact]
    public async Task DisconnectAsync_ClosesConnectionAndSetsIsConnectedFalse()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        bool disconnectedFired = false;
        client.Disconnected += _ =>
        {
            disconnectedFired = true;
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();

        Assert.False(client.IsConnected);
        Assert.True(adapter.IsClosed);
        Assert.True(disconnectedFired);
    }

    [Fact]
    public async Task UnexpectedDisconnect_FiresDisconnectedAndReconnects()
    {
        int adapterCreations = 0;
        FakeWebSocketAdapter? currentAdapter = null;

        var client = new WebSocketClient(
            () =>
            {
                adapterCreations++;
                currentAdapter = new FakeWebSocketAdapter();
                return currentAdapter;
            },
            initialBackoffSeconds: 0.02,
            maxBackoffSeconds: 0.05);

        Exception? disconnectedEx = null;
        var disconnectTcs = new TaskCompletionSource<bool>();

        client.Disconnected += ex =>
        {
            disconnectedEx = ex;
            disconnectTcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");
        Assert.True(client.IsConnected);
        Assert.Equal(1, adapterCreations);

        // Simulate unexpected connection error / network drop
        currentAdapter!.SimulateNetworkDrop(new WebSocketException("Network connection dropped."));

        var completed = await Task.WhenAny(disconnectTcs.Task, Task.Delay(2000));
        Assert.Same(disconnectTcs.Task, completed);
        Assert.NotNull(disconnectedEx);

        // Wait for reconnect with fast backoff (0.02s)
        for (int i = 0; i < 20 && adapterCreations < 2; i++)
        {
            await Task.Delay(50);
        }

        Assert.True(adapterCreations >= 2);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task ReceiveLoop_MultiChunkMessageExceeding64KB_SuccessfullyReassemblesAndDispatches()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        WebSocketMessage? received = null;
        var tcs = new TaskCompletionSource<bool>();

        client.MessageReceived += msg =>
        {
            received = msg;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");

        // Create a 70KB payload string (exceeding 64KB)
        string largePayload = new string('X', 70000);
        var largeMsg = WebSocketMessage.Create("LARGE_DATA", "room_bulk", new { payload = largePayload });
        string serialized = JsonSerializer.Serialize(largeMsg);

        // Deliver in 2048-byte chunks (over 34 chunks)
        adapter.EnqueueChunkedMessage(serialized, 2048);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, completed);
        Assert.NotNull(received);
        Assert.Equal("LARGE_DATA", received.Action);
        Assert.Equal("room_bulk", received.RoomId);

        var extracted = received.GetData<LargeDataPayload>();
        Assert.NotNull(extracted);
        Assert.Equal(70000, extracted.Payload.Length);
    }

    private sealed record LargeDataPayload(string Payload);

    [Fact]
    public async Task HeartbeatFailure_TriggersHandleConnectionFailureAndAutoReconnect()
    {
        int adapterCreations = 0;
        FakeWebSocketAdapter? currentAdapter = null;

        var client = new WebSocketClient(
            () =>
            {
                adapterCreations++;
                currentAdapter = new FakeWebSocketAdapter();
                return currentAdapter;
            },
            heartbeatInterval: TimeSpan.FromMilliseconds(50),
            initialBackoffSeconds: 0.02,
            maxBackoffSeconds: 0.05);

        Exception? disconnectedEx = null;
        var disconnectTcs = new TaskCompletionSource<bool>();

        client.Disconnected += ex =>
        {
            disconnectedEx = ex;
            disconnectTcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");
        Assert.True(client.IsConnected);
        Assert.Equal(1, adapterCreations);

        // Make sending fail on dead socket
        currentAdapter!.FailOnSend = true;

        var completed = await Task.WhenAny(disconnectTcs.Task, Task.Delay(2000));
        Assert.Same(disconnectTcs.Task, completed);
        Assert.NotNull(disconnectedEx);

        // Wait for reconnect attempt
        for (int i = 0; i < 20 && adapterCreations < 2; i++)
        {
            await Task.Delay(50);
        }

        Assert.True(adapterCreations >= 2);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task ReceiveLoop_CorruptedJsonFrame_IgnoredWithoutCrashingReceiveLoop()
    {
        var adapter = new FakeWebSocketAdapter();
        await using var client = new WebSocketClient(() => adapter);

        WebSocketMessage? received = null;
        var tcs = new TaskCompletionSource<bool>();

        client.MessageReceived += msg =>
        {
            received = msg;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new Uri("wss://example.com/ws"), "token");

        // Send invalid JSON frame first
        adapter.EnqueueMessage("{{NOT_A_VALID_JSON_FRAME");

        // Send valid message next
        var validMsg = WebSocketMessage.Create("VALID_ACTION", "room_ok", new { count = 42 });
        adapter.EnqueueMessage(JsonSerializer.Serialize(validMsg));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);
        Assert.NotNull(received);
        Assert.Equal("VALID_ACTION", received.Action);
    }

    [Fact]
    public void WebSocketMessage_GetData_DeserializesCamelCaseJsonWithPascalCaseRecord()
    {
        string json = "{\"action\":\"SYNC\",\"roomId\":\"r1\",\"data\":{\"userId\":\"u123\",\"isActive\":true}}";
        var msg = JsonSerializer.Deserialize<WebSocketMessage>(json);
        Assert.NotNull(msg);

        var data = msg.GetData<TestUserData>();
        Assert.NotNull(data);
        Assert.Equal("u123", data.UserId);
        Assert.True(data.IsActive);
    }

    private sealed record TestUserData(string UserId, bool IsActive);
}

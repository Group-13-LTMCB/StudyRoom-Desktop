using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.Infrastructure.Network;

public class WebSocketClient : IWebSocketClient
{
    private readonly Func<IWebSocketAdapter> _adapterFactory;
    private readonly TimeSpan _heartbeatInterval;
    private readonly double _initialBackoffSeconds;
    private readonly double _maxBackoffSeconds;
    private readonly double _jitterRatio;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private IWebSocketAdapter? _adapter;
    private Uri? _serverUri;
    private string? _accessToken;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _reconnectCts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private int _isReconnecting;
    private bool _isExplicitlyDisconnected;
    private bool _disposed;

    public bool IsConnected => _adapter?.State == WebSocketState.Open;

    public event Func<WebSocketMessage, Task>? MessageReceived;
    public event Func<Exception?, Task>? Disconnected;

    public WebSocketClient(
        TimeSpan? heartbeatInterval = null,
        double initialBackoffSeconds = 1.0,
        double maxBackoffSeconds = 30.0,
        double jitterRatio = 0.2)
        : this(() => new ClientWebSocketAdapter(), heartbeatInterval, initialBackoffSeconds, maxBackoffSeconds, jitterRatio)
    {
    }

    internal WebSocketClient(
        Func<IWebSocketAdapter> adapterFactory,
        TimeSpan? heartbeatInterval = null,
        double initialBackoffSeconds = 1.0,
        double maxBackoffSeconds = 30.0,
        double jitterRatio = 0.2)
    {
        _adapterFactory = adapterFactory;
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30);
        _initialBackoffSeconds = initialBackoffSeconds;
        _maxBackoffSeconds = maxBackoffSeconds;
        _jitterRatio = jitterRatio;
    }

    public static TimeSpan CalculateBackoffWithJitter(
        int attempt,
        double initialSeconds = 1.0,
        double maxSeconds = 30.0,
        double jitterRatio = 0.2,
        Random? random = null)
    {
        if (initialSeconds <= 0) initialSeconds = 1.0;
        if (maxSeconds <= 0) maxSeconds = 30.0;
        if (jitterRatio < 0) jitterRatio = 0.0;

        if (attempt <= 0)
        {
            attempt = 1;
        }

        double expFactor = Math.Pow(2, Math.Min(attempt - 1, 10));
        double baseSeconds = Math.Min(initialSeconds * expFactor, maxSeconds);

        random ??= Random.Shared;
        double jitter = 1.0 + (random.NextDouble() * 2.0 - 1.0) * jitterRatio;
        double minClamp = Math.Min(0.01, maxSeconds);
        double maxClamp = Math.Max(minClamp, maxSeconds * (1.0 + jitterRatio));
        double resultSeconds = Math.Clamp(baseSeconds * jitter, minClamp, maxClamp);

        return TimeSpan.FromSeconds(resultSeconds);
    }

    public async Task ConnectAsync(Uri serverUri, string accessToken, CancellationToken cancellationToken = default)
    {
        _serverUri = serverUri ?? throw new ArgumentNullException(nameof(serverUri));
        _accessToken = accessToken ?? string.Empty;
        _isExplicitlyDisconnected = false;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
        Interlocked.Exchange(ref _isReconnecting, 0);

        await InitializeConnectionAsync(cancellationToken);
    }

    private async Task InitializeConnectionAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _adapter?.Dispose();
        _adapter = _adapterFactory();

        var targetUri = BuildTargetUri(_serverUri!, _accessToken!);
        await _adapter.ConnectAsync(targetUri, cancellationToken);

        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
    }

    public async Task SendAsync(WebSocketMessage message, CancellationToken cancellationToken = default)
    {
        if (_adapter == null || _adapter.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_adapter.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not connected.");
            }

            await _adapter.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _isExplicitlyDisconnected = true;
        _reconnectCts?.Cancel();
        _cts?.Cancel();

        if (_adapter != null && _adapter.State == WebSocketState.Open)
        {
            try
            {
                await _adapter.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", cancellationToken);
            }
            catch
            {
                // Ignored during shutdown
            }
        }

        if (Disconnected != null)
        {
            await Disconnected.Invoke(null);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _adapter != null && _adapter.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await _adapter.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleServerInitiatedCloseAsync(cancellationToken);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text && ms.Length > 0)
                {
                    await DispatchIncomingMessageAsync(ms.ToArray());
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            await HandleConnectionFailureAsync(ex);
        }
    }

    private async Task DispatchIncomingMessageAsync(byte[] bytes)
    {
        try
        {
            var message = JsonSerializer.Deserialize<WebSocketMessage>(bytes);
            if (message == null)
            {
                return;
            }

            if (string.Equals(message.Action, "PING", StringComparison.OrdinalIgnoreCase))
            {
                var pong = WebSocketMessage.Create("PONG", message.RoomId, new { timestamp = DateTimeOffset.UtcNow });
                await SendAsync(pong);
                return;
            }

            if (string.Equals(message.Action, "PONG", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (MessageReceived != null)
            {
                await MessageReceived.Invoke(message);
            }
        }
        catch
        {
            // Ignore corrupted json frames
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_heartbeatInterval, cancellationToken);

                if (IsConnected)
                {
                    var ping = WebSocketMessage.Create("PING", string.Empty, new { timestamp = DateTimeOffset.UtcNow });
                    await SendAsync(ping, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            await HandleConnectionFailureAsync(ex);
        }
    }

    private async Task HandleServerInitiatedCloseAsync(CancellationToken cancellationToken)
    {
        if (_adapter != null && (_adapter.State == WebSocketState.Open || _adapter.State == WebSocketState.CloseReceived))
        {
            try
            {
                await _adapter.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge close", cancellationToken);
            }
            catch
            {
                // Ignored
            }
        }

        if (!_isExplicitlyDisconnected)
        {
            await HandleConnectionFailureAsync(new WebSocketException("Server closed connection."));
        }
    }

    private async Task HandleConnectionFailureAsync(Exception ex)
    {
        if (_isExplicitlyDisconnected || _disposed)
        {
            return;
        }

        if (Disconnected != null)
        {
            try
            {
                await Disconnected.Invoke(ex);
            }
            catch
            {
                // Suppress subscriber exceptions
            }
        }

        if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) == 0)
        {
            _ = Task.Run(() => StartReconnectLoopAsync());
        }
    }

    private async Task StartReconnectLoopAsync()
    {
        int attempt = 0;
        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();

        try
        {
            while (!_isExplicitlyDisconnected && !_disposed)
            {
                attempt++;
                var delay = CalculateBackoffWithJitter(attempt, _initialBackoffSeconds, _maxBackoffSeconds, _jitterRatio);

                try
                {
                    await Task.Delay(delay, _reconnectCts.Token);

                    if (_isExplicitlyDisconnected || _disposed || _serverUri == null)
                    {
                        break;
                    }

                    await InitializeConnectionAsync(CancellationToken.None);

                    if (IsConnected)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Continue to next backoff attempt
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isReconnecting, 0);
        }
    }

    private static Uri BuildTargetUri(Uri serverUri, string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return serverUri;
        }

        var builder = new UriBuilder(serverUri);
        string tokenQuery = $"token={Uri.EscapeDataString(accessToken)}";

        if (string.IsNullOrEmpty(builder.Query) || builder.Query == "?")
        {
            builder.Query = tokenQuery;
        }
        else
        {
            builder.Query = builder.Query.TrimStart('?') + "&" + tokenQuery;
        }

        return builder.Uri;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _isExplicitlyDisconnected = true;
        _cts?.Cancel();

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // Suppress on dispose
            }
        }

        if (_heartbeatTask != null)
        {
            try
            {
                await _heartbeatTask;
            }
            catch
            {
                // Suppress on dispose
            }
        }

        if (_adapter != null)
        {
            _adapter.Dispose();
        }

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _cts?.Dispose();
        _sendLock.Dispose();

        GC.SuppressFinalize(this);
    }
}

public interface IWebSocketAdapter : IDisposable
{
    WebSocketState State { get; }
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken);
}

public sealed class ClientWebSocketAdapter : IWebSocketAdapter
{
    private readonly ClientWebSocket _client = new();

    public WebSocketState State => _client.State;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        return _client.ConnectAsync(uri, cancellationToken);
    }

    public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        return _client.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
    }

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        return _client.ReceiveAsync(buffer, cancellationToken);
    }

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        return _client.CloseAsync(closeStatus, statusDescription, cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

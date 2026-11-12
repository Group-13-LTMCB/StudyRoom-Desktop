using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StudyRoom.Application.Engines;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.ViewModels;

public partial class FloatingBubbleViewModel : ObservableObject, IDisposable
{
    private readonly IRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;
    private readonly IMessenger _messenger;
    private readonly IAudioService? _audioService;
    private readonly IWebSocketRelayService? _webSocketRelayService;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isRateLimited;

    [ObservableProperty]
    private string? _rateLimitWarning;

    [ObservableProperty]
    private TimeSpan _retryAfter = TimeSpan.Zero;

    [ObservableProperty]
    private string _currentUserId = "user_me";

    [ObservableProperty]
    private string _currentUserName = "Tôi";

    [ObservableProperty]
    private bool _isVisible = true;

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public int MessageCount => Messages.Count;

    public bool HasMessages => Messages.Count > 0;

    public event Action? RequestClose;
    public event Action? RequestToggleVisibility;
    public event Action? RequestFocusInput;

    [ActivatorUtilitiesConstructor]
    public FloatingBubbleViewModel(
        IRateLimiter rateLimiter,
        IAudioService? audioService = null,
        IWebSocketRelayService? webSocketRelayService = null) : this(rateLimiter, TimeProvider.System, null, audioService, webSocketRelayService)
    {
    }

    public FloatingBubbleViewModel(IRateLimiter rateLimiter, TimeProvider timeProvider) : this(rateLimiter, timeProvider, null, null, null)
    {
    }

    public FloatingBubbleViewModel(
        IRateLimiter rateLimiter,
        TimeProvider timeProvider,
        IMessenger? messenger,
        IAudioService? audioService = null,
        IWebSocketRelayService? webSocketRelayService = null)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        _audioService = audioService;
        _webSocketRelayService = webSocketRelayService;

        _messenger.Register<ChatMessageReceivedMessage>(this, (r, m) => ((FloatingBubbleViewModel)r).HandleChatMessageReceived(m));
        _messenger.Register<PhaseChangedMessage>(this, (r, m) => ((FloatingBubbleViewModel)r).HandlePhaseChanged(m));

        Messages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(MessageCount));
            OnPropertyChanged(nameof(HasMessages));
        };
    }

    public FloatingBubbleViewModel() : this(new TokenBucketRateLimiter(5, 1), TimeProvider.System, null, null)
    {
    }

    [RelayCommand]
    public void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            return;
        }

        if (!_rateLimiter.TryAcquire())
        {
            IsRateLimited = true;
            RetryAfter = _rateLimiter.GetRetryAfter();
            double seconds = Math.Max(0.1, Math.Round(RetryAfter.TotalSeconds, 1));
            RateLimitWarning = $"Bạn đang gửi tin nhắn quá nhanh. Vui lòng thử lại sau {seconds:0.0} giây!";
            return;
        }

        IsRateLimited = false;
        RateLimitWarning = null;
        RetryAfter = TimeSpan.Zero;

        var senderId = _webSocketRelayService?.UserId is { Length: > 0 } uid ? uid : CurrentUserId;
        var senderName = _webSocketRelayService?.DisplayName is { Length: > 0 } name ? name : CurrentUserName;

        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SenderId = senderId,
            SenderName = senderName,
            Content = InputText.Trim(),
            Timestamp = _timeProvider.GetUtcNow(),
            IsSelf = true
        };

        Messages.Add(message);
        var textToSend = message.Content;
        InputText = string.Empty;

        _messenger.Send(new ChatMessageReceivedMessage(message));
        _ = _webSocketRelayService?.SendChatAsync(textToSend);
    }

    [RelayCommand]
    public void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
        if (IsExpanded)
        {
            RequestFocusInput?.Invoke();
        }
    }

    [RelayCommand]
    public void Close()
    {
        IsVisible = false;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
        RequestToggleVisibility?.Invoke();
    }

    [RelayCommand]
    public void ClearWarning()
    {
        IsRateLimited = false;
        RateLimitWarning = null;
        RetryAfter = TimeSpan.Zero;
    }

    public void ReceiveMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SafeAddMessage(message));
        }
        else
        {
            SafeAddMessage(message);
        }
    }

    private void SafeAddMessage(ChatMessage message)
    {
        try
        {
            Messages.Add(message);
        }
        catch (NotSupportedException)
        {
            // Handled when running in headless test environments with multiple threads
        }
    }

    public void HandleChatMessageReceived(ChatMessageReceivedMessage msg)
    {
        if (msg?.Message == null) return;
        if (msg.Message.SenderId == CurrentUserId && msg.Message.IsSelf) return;
        if (Messages.Any(m => m.MessageId == msg.Message.MessageId)) return;

        _audioService?.PlayMessageNotification();
        ReceiveMessage(msg.Message);
    }

    public void HandlePhaseChanged(PhaseChangedMessage message)
    {
        void ApplyPhase()
        {
            if (message.NewPhase == PomodoroPhase.ShortBreak || message.NewPhase == PomodoroPhase.LongBreak)
            {
                IsVisible = true;
                IsExpanded = true;
                var breakNotice = new ChatMessage("system", "Hệ thống", "☕ Đã đến giờ nghỉ ngơi! Hãy thư giãn và trò chuyện cùng bạn học.", isSelf: false);
                SafeAddMessage(breakNotice);
            }
            else if (message.NewPhase == PomodoroPhase.Focus)
            {
                IsExpanded = false;
                var focusNotice = new ChatMessage("system", "Hệ thống", "🎯 Bắt đầu phiên tập trung mới! Hạn chế trò chuyện để đạt hiệu quả cao nhất.", isSelf: false);
                SafeAddMessage(focusNotice);
            }
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ApplyPhase);
        }
        else
        {
            ApplyPhase();
        }
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}

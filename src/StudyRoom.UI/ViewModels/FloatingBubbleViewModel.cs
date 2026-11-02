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
    public FloatingBubbleViewModel(IRateLimiter rateLimiter) : this(rateLimiter, TimeProvider.System, null)
    {
    }

    public FloatingBubbleViewModel(IRateLimiter rateLimiter, TimeProvider timeProvider) : this(rateLimiter, timeProvider, null)
    {
    }

    public FloatingBubbleViewModel(IRateLimiter rateLimiter, TimeProvider timeProvider, IMessenger? messenger)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        _messenger.Register<ChatMessageReceivedMessage>(this, (r, m) => ((FloatingBubbleViewModel)r).HandleChatMessageReceived(m));
        _messenger.Register<PhaseChangedMessage>(this, (r, m) => ((FloatingBubbleViewModel)r).HandlePhaseChanged(m));

        Messages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(MessageCount));
            OnPropertyChanged(nameof(HasMessages));
        };
    }

    public FloatingBubbleViewModel() : this(new TokenBucketRateLimiter(5, 1), TimeProvider.System, null)
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

        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SenderId = CurrentUserId,
            SenderName = CurrentUserName,
            Content = InputText.Trim(),
            Timestamp = _timeProvider.GetUtcNow(),
            IsSelf = true
        };

        Messages.Add(message);
        InputText = string.Empty;

        _messenger.Send(new ChatMessageReceivedMessage(message));
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

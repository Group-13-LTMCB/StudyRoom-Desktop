using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StudyRoom.Application.Engines;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.ViewModels;

public partial class FloatingBubbleViewModel : ObservableObject
{
    private readonly IRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

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
    public FloatingBubbleViewModel(IRateLimiter rateLimiter) : this(rateLimiter, TimeProvider.System)
    {
    }

    public FloatingBubbleViewModel(IRateLimiter rateLimiter, TimeProvider timeProvider)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _timeProvider = timeProvider ?? TimeProvider.System;

        Messages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(MessageCount));
            OnPropertyChanged(nameof(HasMessages));
        };
    }

    public FloatingBubbleViewModel() : this(new TokenBucketRateLimiter(5, 1), TimeProvider.System)
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
            dispatcher.Invoke(() => Messages.Add(message));
        }
        else
        {
            Messages.Add(message);
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Messages;

namespace StudyRoom.UI.ViewModels;

public partial class FlashcardViewModel : ObservableObject, IRecipient<FlashcardPromptMessage>, IDisposable
{
    private readonly IMessenger? _messenger;
    private CancellationTokenSource? _countdownCts;
    private bool _disposed;

    [ObservableProperty]
    private string _cardId = string.Empty;

    [ObservableProperty]
    private string _question = string.Empty;

    [ObservableProperty]
    private string _answer = string.Empty;

    [ObservableProperty]
    private bool _isFlipped;

    [ObservableProperty]
    private int _totalTimeoutSeconds = 30;

    [ObservableProperty]
    private int _remainingSeconds = 30;

    [ObservableProperty]
    private double _timeProgressPercent = 100.0;

    [ObservableProperty]
    private bool _isAnswered;

    [ObservableProperty]
    private bool? _isCorrect;

    [ObservableProperty]
    private string _statusMessage = "Câu hỏi ôn tập nhóm!";

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isTimerRunning;

    public event Action<bool>? RequestFlipAnimation;
    public event Action? RequestResetCard;
    public event Action? RequestShow;
    public event Action? RequestClose;
    public event Action<string, bool>? AnswerSubmitted;

    public FlashcardViewModel() : this(WeakReferenceMessenger.Default)
    {
    }

    public FlashcardViewModel(IMessenger? messenger)
    {
        _messenger = messenger;
        if (_messenger != null)
        {
            _messenger.Register<FlashcardPromptMessage>(this);
        }
    }

    public void Receive(FlashcardPromptMessage message)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => LoadCard(message.CardId, message.Question, message.Answer, message.TimeoutSeconds));
            return;
        }

        LoadCard(message.CardId, message.Question, message.Answer, message.TimeoutSeconds);
    }

    public void LoadCard(string cardId, string question, string answer, int timeoutSeconds = 30)
    {
        StopTimer();

        CardId = cardId;
        Question = question;
        Answer = answer;
        TotalTimeoutSeconds = Math.Max(1, timeoutSeconds);
        RemainingSeconds = TotalTimeoutSeconds;
        TimeProgressPercent = 100.0;
        IsFlipped = false;
        IsAnswered = false;
        IsCorrect = null;
        StatusMessage = "Câu hỏi ôn tập nhóm!";
        IsOpen = true;

        RequestResetCard?.Invoke();
        RequestShow?.Invoke();
        StartTimer();
    }

    [RelayCommand]
    public void FlipCard()
    {
        IsFlipped = !IsFlipped;
        RequestFlipAnimation?.Invoke(IsFlipped);
    }

    [RelayCommand]
    public void SubmitAnswer(bool correct)
    {
        StopTimer();
        IsAnswered = true;
        IsCorrect = correct;
        StatusMessage = correct
            ? "Tuyệt vời! Bạn đã nắm vững kiến thức này."
            : "Đừng lo, hãy ghi chú để ôn tập lại sau.";

        if (!IsFlipped)
        {
            IsFlipped = true;
            RequestFlipAnimation?.Invoke(true);
        }

        AnswerSubmitted?.Invoke(CardId, correct);
    }

    [RelayCommand]
    public void MarkCorrect() => SubmitAnswer(true);

    [RelayCommand]
    public void MarkIncorrect() => SubmitAnswer(false);

    [RelayCommand]
    public void CloseDialog()
    {
        StopTimer();
        IsOpen = false;
        RequestClose?.Invoke();
    }

    private void StartTimer()
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _countdownCts = new CancellationTokenSource();
        IsTimerRunning = true;

        _ = CountdownLoopAsync(_countdownCts.Token);
    }

    private void StopTimer()
    {
        _countdownCts?.Cancel();
        IsTimerRunning = false;
    }

    private async Task CountdownLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && RemainingSeconds > 0)
            {
                await Task.Delay(1000, cancellationToken);

                void UpdateTick()
                {
                    RemainingSeconds--;
                    TimeProgressPercent = (double)RemainingSeconds / TotalTimeoutSeconds * 100.0;

                    if (RemainingSeconds <= 0)
                    {
                        IsTimerRunning = false;
                        IsAnswered = true;
                        StatusMessage = "Hết thời gian suy nghĩ! Đáp án đã được lật.";
                        if (!IsFlipped)
                        {
                            IsFlipped = true;
                            RequestFlipAnimation?.Invoke(true);
                        }
                    }
                }

                if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(UpdateTick);
                }
                else
                {
                    UpdateTick();
                }

                if (RemainingSeconds <= 0)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal timer cancellation
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopTimer();
        _countdownCts?.Dispose();

        if (_messenger != null)
        {
            _messenger.Unregister<FlashcardPromptMessage>(this);
        }

        GC.SuppressFinalize(this);
    }
}

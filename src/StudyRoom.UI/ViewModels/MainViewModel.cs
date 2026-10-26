using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;

namespace StudyRoom.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPomodoroTimerService _timerService;

    [ObservableProperty]
    private string _formattedTime = "25:00";

    [ObservableProperty]
    private string _phaseText = "Tập trung (Focus)";

    [ObservableProperty]
    private string _cycleText = "Chu kỳ: 0/4";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _remainingSeconds = 1500;

    [ObservableProperty]
    private int _totalSeconds = 1500;

    [ObservableProperty]
    private string _rawPhase = "Focus";

    public event Action? PhaseCompletedCelebration;

    public bool IsStopped => !IsRunning && !IsPaused;

    public MainViewModel(IPomodoroTimerService timerService)
    {
        _timerService = timerService;
        _timerService.Tick += OnTimerTick;
        _timerService.PhaseCompleted += OnPhaseCompleted;
        UpdateStateFromSession(_timerService.CurrentSession);
    }

    [RelayCommand]
    private void Start()
    {
        _timerService.Start();
        UpdateStateFromSession(_timerService.CurrentSession);
    }

    [RelayCommand]
    private void Pause()
    {
        _timerService.Pause();
        UpdateStateFromSession(_timerService.CurrentSession);
    }

    [RelayCommand]
    private void Resume()
    {
        _timerService.Resume();
        UpdateStateFromSession(_timerService.CurrentSession);
    }

    [RelayCommand]
    private void Reset()
    {
        _timerService.Reset();
        UpdateStateFromSession(_timerService.CurrentSession);
    }

    [RelayCommand]
    private void Skip()
    {
        _timerService.Skip();
        UpdateStateFromSession(_timerService.CurrentSession);
    }

    private void OnTimerTick(PomodoroSession session)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => UpdateStateFromSession(session));
        }
        else
        {
            UpdateStateFromSession(session);
        }
    }

    private void OnPhaseCompleted(PomodoroSession session)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() =>
            {
                UpdateStateFromSession(session);
                PhaseCompletedCelebration?.Invoke();
            });
        }
        else
        {
            UpdateStateFromSession(session);
            PhaseCompletedCelebration?.Invoke();
        }
    }

    private void UpdateStateFromSession(PomodoroSession session)
    {
        RemainingSeconds = session.RemainingSeconds;
        TotalSeconds = session.CurrentPhaseDurationSeconds;
        RawPhase = session.Phase.ToString();

        int minutes = session.RemainingSeconds / 60;
        int seconds = session.RemainingSeconds % 60;
        FormattedTime = $"{minutes:D2}:{seconds:D2}";

        PhaseText = session.Phase switch
        {
            PomodoroPhase.Focus => "🎯 Tập trung (Focus)",
            PomodoroPhase.ShortBreak => "☕ Nghỉ ngắn (Short Break)",
            PomodoroPhase.LongBreak => "🌴 Nghỉ dài (Long Break)",
            _ => "Tập trung"
        };

        int cycleInFour = (session.CompletedFocusCycles % session.CyclesBeforeLongBreak);
        CycleText = $"Chu kỳ hoàn thành: {session.CompletedFocusCycles} (Vòng hiện tại: {cycleInFour}/{session.CyclesBeforeLongBreak})";

        IsRunning = session.State == TimerState.Running;
        IsPaused = session.State == TimerState.Paused;
        OnPropertyChanged(nameof(IsStopped));
    }
}

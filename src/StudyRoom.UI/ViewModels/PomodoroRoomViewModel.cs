using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Application.Engines;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.ViewModels;

public partial class PomodoroRoomViewModel : ObservableObject
{
    private readonly IPomodoroEngine _engine;

    [ObservableProperty]
    private string _roomId = "948210";

    [ObservableProperty]
    private string _roomName = "Toán cao cấp A1 - Nhóm 3";

    [ObservableProperty]
    private string _subject = "Giải tích 2 & Đại số tuyến tính";

    [ObservableProperty]
    private string _formattedTime = "25:00";

    [ObservableProperty]
    private string _phaseText = "🎯 Pha Tập Trung (Focus)";

    [ObservableProperty]
    private string _phaseBadgeColor = "#6366F1";

    [ObservableProperty]
    private string _cycleText = "Chu kỳ: 1 / 4";

    [ObservableProperty]
    private int _remainingSeconds = 1500;

    [ObservableProperty]
    private int _totalSeconds = 1500;

    [ObservableProperty]
    private double _progress = 1.0;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _canControl = true;

    [ObservableProperty]
    private string _controlModeText = "Chế độ: Trưởng nhóm điều khiển (Host-Only)";

    [ObservableProperty]
    private ObservableCollection<RoomMemberModel> _members = [];

    public bool IsStopped => !IsRunning && !IsPaused;

    public event Action? RequestBackToDashboard;

    public PomodoroRoomViewModel(IPomodoroEngine? engine = null)
    {
        _engine = engine ?? new PomodoroEngine();
        _engine.Tick += OnEngineTick;
        _engine.PhaseCompleted += OnEnginePhaseCompleted;

        InitializeDefaultMembers();
        SyncWithEngineSession(_engine.CurrentSession);
    }

    public void SetRoom(StudyRoomSummary room)
    {
        RoomId = room.RoomId;
        RoomName = room.RoomName;
        Subject = room.Subject;
    }

    private void InitializeDefaultMembers()
    {
        Members = [
            new RoomMemberModel
            {
                UserId = "user-1",
                Name = "Sally (Bạn)",
                AvatarEmoji = "👩‍🎓",
                IsHost = true,
                HasBaton = true,
                StatusText = "Đang tập trung 🔥",
                IsFocusing = true
            },
            new RoomMemberModel
            {
                UserId = "user-2",
                Name = "Trần Minh",
                AvatarEmoji = "👨‍💻",
                IsHost = false,
                HasBaton = false,
                StatusText = "Đang tập trung 🔥",
                IsFocusing = true
            },
            new RoomMemberModel
            {
                UserId = "user-3",
                Name = "Lê Thảo",
                AvatarEmoji = "👩‍🎨",
                IsHost = false,
                HasBaton = false,
                StatusText = "Đang tập trung 🔥",
                IsFocusing = true
            },
            new RoomMemberModel
            {
                UserId = "user-4",
                Name = "Hoàng Nam",
                AvatarEmoji = "🧑‍🔬",
                IsHost = false,
                HasBaton = false,
                StatusText = "Đang ghi chép 📝",
                IsFocusing = true
            }
        ];
    }

    [RelayCommand]
    public void Start()
    {
        _engine.Start();
        SyncWithEngineSession(_engine.CurrentSession);
    }

    [RelayCommand]
    public void Pause()
    {
        _engine.Pause();
        SyncWithEngineSession(_engine.CurrentSession);
    }

    [RelayCommand]
    public void Resume()
    {
        _engine.Resume();
        SyncWithEngineSession(_engine.CurrentSession);
    }

    [RelayCommand]
    public void Reset()
    {
        _engine.Reset();
        SyncWithEngineSession(_engine.CurrentSession);
    }

    [RelayCommand]
    public void Skip()
    {
        _engine.SkipPhase();
        SyncWithEngineSession(_engine.CurrentSession);
    }

    [RelayCommand]
    public void BackToDashboard()
    {
        RequestBackToDashboard?.Invoke();
    }

    [RelayCommand]
    public void PassBaton(string targetUserId)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return;
        }

        _engine.PassBaton(targetUserId);

        foreach (var member in Members)
        {
            member.HasBaton = member.UserId == targetUserId;
        }
    }

    private void OnEngineTick(PomodoroSession session)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => SyncWithEngineSession(session));
        }
        else
        {
            SyncWithEngineSession(session);
        }
    }

    private void OnEnginePhaseCompleted(PomodoroSession session)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => SyncWithEngineSession(session));
        }
        else
        {
            SyncWithEngineSession(session);
        }
    }

    public void SyncWithEngineSession(PomodoroSession session)
    {
        RemainingSeconds = session.RemainingSeconds;
        TotalSeconds = session.CurrentPhaseDurationSeconds;

        int minutes = session.RemainingSeconds / 60;
        int seconds = session.RemainingSeconds % 60;
        FormattedTime = $"{minutes:D2}:{seconds:D2}";

        IsRunning = session.State == TimerState.Running;
        IsPaused = session.State == TimerState.Paused;
        OnPropertyChanged(nameof(IsStopped));

        Progress = TotalSeconds > 0 ? (double)RemainingSeconds / TotalSeconds : 0.0;

        switch (session.Phase)
        {
            case PomodoroPhase.Focus:
                PhaseText = "🎯 Pha Tập Trung (Focus)";
                PhaseBadgeColor = "#6366F1";
                break;
            case PomodoroPhase.ShortBreak:
                PhaseText = "☕ Giờ Nghỉ Ngắn (Short Break)";
                PhaseBadgeColor = "#10B981";
                break;
            case PomodoroPhase.LongBreak:
                PhaseText = "🌴 Giờ Nghỉ Dài (Long Break)";
                PhaseBadgeColor = "#F59E0B";
                break;
        }

        int cycleNumber = (session.CompletedFocusCycles % session.CyclesBeforeLongBreak) + 1;
        CycleText = $"Chu kỳ: {cycleNumber} / {session.CyclesBeforeLongBreak}";
    }
}

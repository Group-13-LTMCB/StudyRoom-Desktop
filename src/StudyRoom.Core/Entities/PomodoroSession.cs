using StudyRoom.Core.Enums;

namespace StudyRoom.Core.Entities;

public class PomodoroSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? GroupId { get; set; }
    public Guid? BatonHolderUserId { get; set; }

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;
    public TimerState State { get; private set; } = TimerState.Stopped;

    public int FocusDurationSeconds { get; set; } = 25 * 60;
    public int ShortBreakDurationSeconds { get; set; } = 5 * 60;
    public int LongBreakDurationSeconds { get; set; } = 15 * 60;
    public int CyclesBeforeLongBreak { get; set; } = 4;

    public int RemainingSeconds { get; private set; } = 25 * 60;
    public int CompletedFocusCycles { get; private set; }

    public int CurrentPhaseDurationSeconds => Phase switch
    {
        PomodoroPhase.Focus => FocusDurationSeconds,
        PomodoroPhase.ShortBreak => ShortBreakDurationSeconds,
        PomodoroPhase.LongBreak => LongBreakDurationSeconds,
        _ => FocusDurationSeconds
    };

    public void Start()
    {
        if (State == TimerState.Stopped)
        {
            RemainingSeconds = CurrentPhaseDurationSeconds;
        }
        State = TimerState.Running;
    }

    public void Pause()
    {
        if (State == TimerState.Running)
        {
            State = TimerState.Paused;
        }
    }

    public void Resume()
    {
        if (State == TimerState.Paused)
        {
            State = TimerState.Running;
        }
    }

    public void Reset()
    {
        Phase = PomodoroPhase.Focus;
        State = TimerState.Stopped;
        RemainingSeconds = FocusDurationSeconds;
        CompletedFocusCycles = 0;
    }

    public bool Tick()
    {
        if (State != TimerState.Running)
        {
            return false;
        }

        if (RemainingSeconds > 0)
        {
            RemainingSeconds--;
        }

        if (RemainingSeconds == 0)
        {
            AdvanceToNextPhase();
            return true;
        }

        return false;
    }

    public void SkipPhase()
    {
        AdvanceToNextPhase();
    }

    public void PassBaton(Guid newHolderUserId)
    {
        BatonHolderUserId = newHolderUserId;
    }

    private void AdvanceToNextPhase()
    {
        if (Phase == PomodoroPhase.Focus)
        {
            CompletedFocusCycles++;
            Phase = (CompletedFocusCycles % CyclesBeforeLongBreak == 0)
                ? PomodoroPhase.LongBreak
                : PomodoroPhase.ShortBreak;
        }
        else
        {
            Phase = PomodoroPhase.Focus;
        }

        RemainingSeconds = CurrentPhaseDurationSeconds;
    }
}

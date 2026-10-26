using StudyRoom.Core.Enums;

namespace StudyRoom.Core.Entities;

public class PomodoroSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? GroupId { get; set; }
    public Guid? BatonHolderUserId { get; set; }

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;
    public TimerState State { get; private set; } = TimerState.Stopped;
    public DateTimeOffset? TargetEndTimeUtc { get; set; }

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

    public void Start(DateTimeOffset? targetEndTime = null)
    {
        if (State == TimerState.Stopped)
        {
            RemainingSeconds = CurrentPhaseDurationSeconds;
        }

        TargetEndTimeUtc = targetEndTime ?? DateTimeOffset.UtcNow.AddSeconds(RemainingSeconds);
        State = TimerState.Running;
    }

    public void Pause()
    {
        if (State == TimerState.Running)
        {
            if (TargetEndTimeUtc.HasValue)
            {
                RemainingSeconds = Math.Max(0, (int)Math.Ceiling((TargetEndTimeUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
                TargetEndTimeUtc = null;
            }

            State = TimerState.Paused;
        }
    }

    public void Resume(DateTimeOffset? targetEndTime = null)
    {
        if (State == TimerState.Paused)
        {
            TargetEndTimeUtc = targetEndTime ?? DateTimeOffset.UtcNow.AddSeconds(RemainingSeconds);
            State = TimerState.Running;
        }
    }

    public void Reset()
    {
        Phase = PomodoroPhase.Focus;
        State = TimerState.Stopped;
        RemainingSeconds = FocusDurationSeconds;
        TargetEndTimeUtc = null;
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

    public void UpdateRemainingSeconds(int seconds)
    {
        RemainingSeconds = Math.Max(0, seconds);
    }

    public void SyncState(DateTimeOffset? targetEndTimeUtc, PomodoroPhase phase, TimerState state, int? remainingSeconds = null)
    {
        TargetEndTimeUtc = targetEndTimeUtc;
        Phase = phase;
        State = state;

        if (remainingSeconds.HasValue)
        {
            RemainingSeconds = Math.Max(0, remainingSeconds.Value);
        }
        else if (targetEndTimeUtc.HasValue && state == TimerState.Running)
        {
            RemainingSeconds = Math.Max(0, (int)Math.Ceiling((targetEndTimeUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
        }
    }

    public void SkipPhase()
    {
        AdvanceToNextPhase();
    }

    public void PassBaton(Guid newHolderUserId)
    {
        BatonHolderUserId = newHolderUserId;
    }

    public void AdvanceToNextPhase()
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

        if (State == TimerState.Running)
        {
            TargetEndTimeUtc = DateTimeOffset.UtcNow.AddSeconds(RemainingSeconds);
        }
        else
        {
            TargetEndTimeUtc = null;
        }
    }
}

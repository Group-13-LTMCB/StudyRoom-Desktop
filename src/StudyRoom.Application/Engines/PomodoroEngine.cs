using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.Application.Engines;

public sealed class PomodoroEngine : IPomodoroEngine
{
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _timerTask;
    private readonly object _lock = new();

    public PomodoroSession CurrentSession { get; }

    public event Action<PomodoroSession>? Tick;
    public event Action<PomodoroSession>? PhaseCompleted;

    public PomodoroEngine(PomodoroSession? session = null, TimeSpan? timerPeriod = null)
    {
        CurrentSession = session ?? new PomodoroSession();
        _timer = new PeriodicTimer(timerPeriod ?? TimeSpan.FromMilliseconds(200));
        _timerTask = RunTimerLoopAsync(_cts.Token);
    }

    public void Start()
    {
        lock (_lock)
        {
            CurrentSession.Start();
        }

        Tick?.Invoke(CurrentSession);
    }

    public void Pause()
    {
        lock (_lock)
        {
            CurrentSession.Pause();
        }

        Tick?.Invoke(CurrentSession);
    }

    public void Resume()
    {
        lock (_lock)
        {
            CurrentSession.Resume();
        }

        Tick?.Invoke(CurrentSession);
    }

    public void Reset()
    {
        lock (_lock)
        {
            CurrentSession.Reset();
        }

        Tick?.Invoke(CurrentSession);
    }

    public void SkipPhase()
    {
        lock (_lock)
        {
            CurrentSession.SkipPhase();
        }

        Tick?.Invoke(CurrentSession);
        PhaseCompleted?.Invoke(CurrentSession);
    }

    public void PassBaton(string newHolderUserId)
    {
        lock (_lock)
        {
            if (Guid.TryParse(newHolderUserId, out var guid))
            {
                CurrentSession.PassBaton(guid);
            }
        }
    }

    public bool CanControl(string currentUserId, StudyGroup group)
    {
        if (group.ControlMode == ControlMode.Equal)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(group.CurrentHostId) && group.CurrentHostId == currentUserId)
        {
            return true;
        }

        if (Guid.TryParse(currentUserId, out var userGuid))
        {
            if (group.HostUserId == userGuid)
            {
                return true;
            }

            if (CurrentSession.BatonHolderUserId == userGuid)
            {
                return true;
            }
        }

        return false;
    }

    public void SyncFromServer(DateTimeOffset? targetEndTimeUtc, PomodoroPhase phase, TimerState state)
    {
        bool changed;
        lock (_lock)
        {
            int? calculatedRemaining = null;
            if (targetEndTimeUtc.HasValue && state == TimerState.Running)
            {
                calculatedRemaining = Math.Max(0, (int)Math.Ceiling((targetEndTimeUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
            }

            // Sync if state/phase changed or clock drift > 500ms (1 second in integer seconds)
            bool driftDetected = calculatedRemaining.HasValue &&
                                 Math.Abs(calculatedRemaining.Value - CurrentSession.RemainingSeconds) >= 1;

            changed = CurrentSession.Phase != phase ||
                      CurrentSession.State != state ||
                      driftDetected;

            if (changed)
            {
                CurrentSession.SyncState(targetEndTimeUtc, phase, state, calculatedRemaining);
            }
        }

        if (changed)
        {
            Tick?.Invoke(CurrentSession);
        }
    }

    private async Task RunTimerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && await _timer.WaitForNextTickAsync(cancellationToken))
            {
                bool phaseEnded = false;
                lock (_lock)
                {
                    if (CurrentSession.State != TimerState.Running)
                    {
                        continue;
                    }

                    if (CurrentSession.TargetEndTimeUtc.HasValue)
                    {
                        int remaining = (int)Math.Ceiling((CurrentSession.TargetEndTimeUtc.Value - DateTimeOffset.UtcNow).TotalSeconds);
                        if (remaining <= 0)
                        {
                            CurrentSession.AdvanceToNextPhase();
                            phaseEnded = true;
                        }
                        else
                        {
                            CurrentSession.UpdateRemainingSeconds(remaining);
                        }
                    }
                    else
                    {
                        phaseEnded = CurrentSession.Tick();
                    }
                }

                Tick?.Invoke(CurrentSession);

                if (phaseEnded)
                {
                    PhaseCompleted?.Invoke(CurrentSession);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Dispose
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();
        _cts.Dispose();
    }
}

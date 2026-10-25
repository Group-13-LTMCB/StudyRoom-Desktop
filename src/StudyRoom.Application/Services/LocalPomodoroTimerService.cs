using System.Timers;
using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Entities;
using Timer = System.Timers.Timer;

namespace StudyRoom.Application.Services;

public sealed class LocalPomodoroTimerService : IPomodoroTimerService, IDisposable
{
    private readonly Timer _timer;
    private readonly object _lock = new();

    public PomodoroSession CurrentSession { get; }

    public event Action<PomodoroSession>? Tick;
    public event Action<PomodoroSession>? PhaseCompleted;

    public LocalPomodoroTimerService(PomodoroSession? session = null)
    {
        CurrentSession = session ?? new PomodoroSession();
        _timer = new Timer(1000)
        {
            AutoReset = true
        };
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start()
    {
        lock (_lock)
        {
            CurrentSession.Start();
            _timer.Start();
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            CurrentSession.Pause();
            _timer.Stop();
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            CurrentSession.Resume();
            _timer.Start();
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _timer.Stop();
            CurrentSession.Reset();
        }
    }

    public void Skip()
    {
        lock (_lock)
        {
            CurrentSession.SkipPhase();
        }
    }

    public void PassBaton(Guid newHolderUserId)
    {
        lock (_lock)
        {
            CurrentSession.PassBaton(newHolderUserId);
        }
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        bool phaseEnded;
        lock (_lock)
        {
            phaseEnded = CurrentSession.Tick();
        }

        Tick?.Invoke(CurrentSession);

        if (phaseEnded)
        {
            PhaseCompleted?.Invoke(CurrentSession);
        }
    }

    public void Dispose()
    {
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Stop();
        _timer.Dispose();
    }
}

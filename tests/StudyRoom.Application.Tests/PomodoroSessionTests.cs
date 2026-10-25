using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using Xunit;

namespace StudyRoom.Application.Tests;

public class PomodoroSessionTests
{
    [Fact]
    public void NewSession_ShouldHaveDefaultValues()
    {
        var session = new PomodoroSession();

        Assert.Equal(PomodoroPhase.Focus, session.Phase);
        Assert.Equal(TimerState.Stopped, session.State);
        Assert.Equal(25 * 60, session.RemainingSeconds);
        Assert.Equal(0, session.CompletedFocusCycles);
    }

    [Fact]
    public void Start_ShouldTransitionStateToRunning()
    {
        var session = new PomodoroSession();
        session.Start();

        Assert.Equal(TimerState.Running, session.State);
    }

    [Fact]
    public void PauseAndResume_ShouldUpdateTimerState()
    {
        var session = new PomodoroSession();
        session.Start();
        session.Pause();

        Assert.Equal(TimerState.Paused, session.State);

        session.Resume();
        Assert.Equal(TimerState.Running, session.State);
    }

    [Fact]
    public void Reset_ShouldRestoreInitialFocusState()
    {
        var session = new PomodoroSession();
        session.Start();
        session.Tick();
        session.SkipPhase();

        session.Reset();

        Assert.Equal(PomodoroPhase.Focus, session.Phase);
        Assert.Equal(TimerState.Stopped, session.State);
        Assert.Equal(25 * 60, session.RemainingSeconds);
        Assert.Equal(0, session.CompletedFocusCycles);
    }

    [Fact]
    public void Tick_WhenRunning_ShouldDecrementRemainingSeconds()
    {
        var session = new PomodoroSession();
        session.Start();

        int initialSeconds = session.RemainingSeconds;
        bool phaseEnded = session.Tick();

        Assert.False(phaseEnded);
        Assert.Equal(initialSeconds - 1, session.RemainingSeconds);
    }

    [Fact]
    public void Tick_WhenTimeReachesZero_ShouldAdvanceToShortBreak()
    {
        var session = new PomodoroSession
        {
            FocusDurationSeconds = 2,
            ShortBreakDurationSeconds = 1
        };
        session.Start();

        // 2 -> 1
        session.Tick();
        // 1 -> 0 => transition to ShortBreak
        bool phaseEnded = session.Tick();

        Assert.True(phaseEnded);
        Assert.Equal(PomodoroPhase.ShortBreak, session.Phase);
        Assert.Equal(1, session.RemainingSeconds);
        Assert.Equal(1, session.CompletedFocusCycles);
    }

    [Fact]
    public void CompletingFourCycles_ShouldAdvanceToLongBreak()
    {
        var session = new PomodoroSession
        {
            FocusDurationSeconds = 1,
            ShortBreakDurationSeconds = 1,
            LongBreakDurationSeconds = 3,
            CyclesBeforeLongBreak = 4
        };

        for (int i = 0; i < 3; i++)
        {
            session.Start();
            session.Tick(); // Focus ends -> ShortBreak
            Assert.Equal(PomodoroPhase.ShortBreak, session.Phase);

            session.Tick(); // ShortBreak ends -> Focus
            Assert.Equal(PomodoroPhase.Focus, session.Phase);
        }

        // 4th cycle
        session.Tick(); // Focus ends -> Should be LongBreak
        Assert.Equal(PomodoroPhase.LongBreak, session.Phase);
        Assert.Equal(4, session.CompletedFocusCycles);
        Assert.Equal(3, session.RemainingSeconds);
    }

    [Fact]
    public void PassBaton_ShouldUpdateBatonHolderUserId()
    {
        var session = new PomodoroSession();
        var newHolder = Guid.NewGuid();

        session.PassBaton(newHolder);

        Assert.Equal(newHolder, session.BatonHolderUserId);
    }
}

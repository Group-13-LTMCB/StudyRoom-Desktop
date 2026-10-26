using StudyRoom.Application.Engines;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;

namespace StudyRoom.Application.Tests;

public class PomodoroEngineTests
{
    [Fact]
    public void Start_ShouldSetRunningAndTargetEndTime()
    {
        using var engine = new PomodoroEngine();
        engine.Start();

        Assert.Equal(TimerState.Running, engine.CurrentSession.State);
        Assert.NotNull(engine.CurrentSession.TargetEndTimeUtc);
    }

    [Fact]
    public void PauseAndResume_ShouldManageStateAndRemainingTime()
    {
        using var engine = new PomodoroEngine();
        engine.Start();
        engine.Pause();

        Assert.Equal(TimerState.Paused, engine.CurrentSession.State);
        Assert.Null(engine.CurrentSession.TargetEndTimeUtc);

        engine.Resume();
        Assert.Equal(TimerState.Running, engine.CurrentSession.State);
        Assert.NotNull(engine.CurrentSession.TargetEndTimeUtc);
    }

    [Fact]
    public void Reset_ShouldResetToDefaultFocusState()
    {
        using var engine = new PomodoroEngine();
        engine.Start();
        engine.SkipPhase();
        engine.Reset();

        Assert.Equal(PomodoroPhase.Focus, engine.CurrentSession.Phase);
        Assert.Equal(TimerState.Stopped, engine.CurrentSession.State);
        Assert.Equal(25 * 60, engine.CurrentSession.RemainingSeconds);
    }

    [Fact]
    public void SkipPhase_ShouldAdvancePhaseAndFirePhaseCompleted()
    {
        using var engine = new PomodoroEngine();
        bool phaseCompletedFired = false;
        engine.PhaseCompleted += _ => phaseCompletedFired = true;

        engine.SkipPhase();

        Assert.True(phaseCompletedFired);
        Assert.Equal(PomodoroPhase.ShortBreak, engine.CurrentSession.Phase);
    }

    [Fact]
    public void PassBaton_ShouldUpdateBatonHolder()
    {
        using var engine = new PomodoroEngine();
        var newHolderId = Guid.NewGuid();

        engine.PassBaton(newHolderId.ToString());

        Assert.Equal(newHolderId, engine.CurrentSession.BatonHolderUserId);
    }

    [Fact]
    public void CanControl_WhenEqualMode_ShouldAllowAnyone()
    {
        using var engine = new PomodoroEngine();
        var group = new StudyGroup
        {
            HostUserId = Guid.NewGuid(),
            ControlMode = ControlMode.Equal
        };

        var randomUserId = Guid.NewGuid().ToString();
        Assert.True(engine.CanControl(randomUserId, group));
    }

    [Fact]
    public void CanControl_WhenHostOnlyMode_EnforcesPermissions()
    {
        using var engine = new PomodoroEngine();
        var hostId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var batonId = Guid.NewGuid();

        var group = new StudyGroup
        {
            HostUserId = hostId,
            ControlMode = ControlMode.HostOnly
        };

        // Host has permission
        Assert.True(engine.CanControl(hostId.ToString(), group));

        // Regular member does not have permission
        Assert.False(engine.CanControl(memberId.ToString(), group));

        // Pass baton to member -> now member has permission
        engine.PassBaton(batonId.ToString());
        Assert.True(engine.CanControl(batonId.ToString(), group));
    }

    [Fact]
    public void SyncFromServer_ShouldApplyTargetTimeAndRecalculate()
    {
        using var engine = new PomodoroEngine();
        var futureTarget = DateTimeOffset.UtcNow.AddSeconds(600);

        engine.SyncFromServer(futureTarget, PomodoroPhase.Focus, TimerState.Running);

        Assert.Equal(TimerState.Running, engine.CurrentSession.State);
        Assert.Equal(PomodoroPhase.Focus, engine.CurrentSession.Phase);
        Assert.InRange(engine.CurrentSession.RemainingSeconds, 598, 601);
    }

    [Fact]
    public async Task TimerLoop_WhenTargetTimeExpires_AdvancesPhaseAutomatically()
    {
        var session = new PomodoroSession
        {
            FocusDurationSeconds = 1,
            ShortBreakDurationSeconds = 2
        };

        // 50ms periodic timer for fast test execution
        using var engine = new PomodoroEngine(session, TimeSpan.FromMilliseconds(50));
        var phaseCompletedTcs = new TaskCompletionSource<bool>();
        engine.PhaseCompleted += _ => phaseCompletedTcs.TrySetResult(true);

        session.Start(DateTimeOffset.UtcNow.AddMilliseconds(150));

        var completed = await Task.WhenAny(phaseCompletedTcs.Task, Task.Delay(2000));
        Assert.Same(phaseCompletedTcs.Task, completed);
        Assert.Equal(PomodoroPhase.ShortBreak, engine.CurrentSession.Phase);
    }
}

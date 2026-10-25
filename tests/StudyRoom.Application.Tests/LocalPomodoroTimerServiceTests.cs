using StudyRoom.Application.Services;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using Xunit;

namespace StudyRoom.Application.Tests;

public class LocalPomodoroTimerServiceTests
{
    [Fact]
    public void Start_ChangesSessionStateToRunning()
    {
        var session = new PomodoroSession();
        using var service = new LocalPomodoroTimerService(session);

        service.Start();

        Assert.Equal(TimerState.Running, service.CurrentSession.State);
    }

    [Fact]
    public void Pause_ChangesSessionStateToPaused()
    {
        var session = new PomodoroSession();
        using var service = new LocalPomodoroTimerService(session);

        service.Start();
        service.Pause();

        Assert.Equal(TimerState.Paused, service.CurrentSession.State);
    }

    [Fact]
    public void Reset_RestoresSessionToStopped()
    {
        var session = new PomodoroSession();
        using var service = new LocalPomodoroTimerService(session);

        service.Start();
        service.Reset();

        Assert.Equal(TimerState.Stopped, service.CurrentSession.State);
    }
}

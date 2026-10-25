using StudyRoom.Application.Services;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class MainViewModelTests
{
    [Fact]
    public void ViewModel_InitialState_ShouldBeStoppedAt25Minutes()
    {
        var session = new PomodoroSession();
        using var service = new LocalPomodoroTimerService(session);
        var viewModel = new MainViewModel(service);

        Assert.Equal("25:00", viewModel.FormattedTime);
        Assert.True(viewModel.IsStopped);
        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.IsPaused);
    }

    [Fact]
    public void StartCommand_ShouldUpdateViewModelStateToRunning()
    {
        var session = new PomodoroSession();
        using var service = new LocalPomodoroTimerService(session);
        var viewModel = new MainViewModel(service);

        viewModel.StartCommand.Execute(null);

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.IsStopped);
    }

    [Fact]
    public void PauseCommand_ShouldUpdateViewModelStateToPaused()
    {
        var session = new PomodoroSession();
        using var service = new LocalPomodoroTimerService(session);
        var viewModel = new MainViewModel(service);

        viewModel.StartCommand.Execute(null);
        viewModel.PauseCommand.Execute(null);

        Assert.True(viewModel.IsPaused);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public void ResetCommand_ShouldResetFormattedTimeToInitial()
    {
        var session = new PomodoroSession
        {
            FocusDurationSeconds = 10
        };
        using var service = new LocalPomodoroTimerService(session);
        var viewModel = new MainViewModel(service);

        viewModel.StartCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);

        Assert.True(viewModel.IsStopped);
        Assert.Equal("00:10", viewModel.FormattedTime);
    }

    [Fact]
    public void SkipCommand_ShouldAdvancePhaseToBreak()
    {
        var session = new PomodoroSession();
        using var service = new LocalPomodoroTimerService(session);
        var viewModel = new MainViewModel(service);

        viewModel.SkipCommand.Execute(null);

        Assert.Contains("Nghỉ ngắn", viewModel.PhaseText);
        Assert.Equal("05:00", viewModel.FormattedTime);
    }
}

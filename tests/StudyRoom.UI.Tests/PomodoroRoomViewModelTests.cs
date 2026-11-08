using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Engines;
using StudyRoom.Application.Messages;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class PomodoroRoomViewModelTests
{
    private static (PomodoroRoomViewModel ViewModel, PomodoroEngine Engine) CreateViewModel(int focusMinutes = 25)
    {
        var session = new PomodoroSession
        {
            FocusDurationSeconds = focusMinutes * 60,
            ShortBreakDurationSeconds = 5 * 60
        };
        session.Reset();

        var engine = new PomodoroEngine(session, TimeSpan.FromMilliseconds(50));
        var vm = new PomodoroRoomViewModel(engine);
        return (vm, engine);
    }

    [Fact]
    public void PomodoroRoomViewModel_InitialState_IsStoppedAndHasDefaultTime()
    {
        var (vm, _) = CreateViewModel(25);

        Assert.True(vm.IsStopped);
        Assert.False(vm.IsRunning);
        Assert.False(vm.IsPaused);
        Assert.Equal("25:00", vm.FormattedTime);
        Assert.Equal(1500, vm.RemainingSeconds);
        Assert.Equal(1500, vm.TotalSeconds);
        Assert.Equal(1.0, vm.Progress);
        Assert.Contains("Focus", vm.PhaseText);
        Assert.NotEmpty(vm.Members);
    }

    [Fact]
    public void PomodoroRoomViewModel_Start_SetsRunningState()
    {
        var (vm, _) = CreateViewModel(25);

        vm.Start();

        Assert.True(vm.IsRunning);
        Assert.False(vm.IsPaused);
        Assert.False(vm.IsStopped);
    }

    [Fact]
    public void PomodoroRoomViewModel_PauseAndResume_TogglesPausedState()
    {
        var (vm, _) = CreateViewModel(25);

        vm.Start();
        vm.Pause();

        Assert.False(vm.IsRunning);
        Assert.True(vm.IsPaused);
        Assert.False(vm.IsStopped);

        vm.Resume();

        Assert.True(vm.IsRunning);
        Assert.False(vm.IsPaused);
    }

    [Fact]
    public void PomodoroRoomViewModel_Reset_RestoresStoppedState()
    {
        var (vm, _) = CreateViewModel(25);

        vm.Start();
        vm.Reset();

        Assert.True(vm.IsStopped);
        Assert.False(vm.IsRunning);
        Assert.False(vm.IsPaused);
        Assert.Equal("25:00", vm.FormattedTime);
        Assert.Equal(1.0, vm.Progress);
    }

    [Fact]
    public void PomodoroRoomViewModel_SkipPhase_AdvancesToBreak()
    {
        var (vm, _) = CreateViewModel(25);

        vm.Start();
        vm.Skip();

        Assert.Contains("Break", vm.PhaseText);
        Assert.Equal(300, vm.RemainingSeconds); // 5 min break
        Assert.Equal("05:00", vm.FormattedTime);
    }

    [Fact]
    public void PomodoroRoomViewModel_SetRoom_UpdatesRoomInformation()
    {
        var (vm, _) = CreateViewModel(25);
        var room = new StudyRoomSummary
        {
            RoomId = "777888",
            RoomName = "Học Nhóm Hóa Học",
            Subject = "Hóa Đại Cương"
        };

        vm.SetRoom(room);

        Assert.Equal("777888", vm.RoomId);
        Assert.Equal("Học Nhóm Hóa Học", vm.RoomName);
        Assert.Equal("Hóa Đại Cương", vm.Subject);
    }

    [Fact]
    public void PomodoroRoomViewModel_PassBaton_UpdatesBatonOwnership()
    {
        var (vm, _) = CreateViewModel(25);

        vm.PassBaton("user-2");

        var user2 = Assert.Single(vm.Members, m => m.UserId == "user-2");
        Assert.True(user2.HasBaton);

        var user1 = Assert.Single(vm.Members, m => m.UserId == "user-1");
        Assert.False(user1.HasBaton);
    }

    [Fact]
    public void PomodoroRoomViewModel_BackToDashboard_InvokesEvent()
    {
        var (vm, _) = CreateViewModel(25);
        bool backTriggered = false;
        vm.RequestBackToDashboard += () => backTriggered = true;

        vm.BackToDashboard();

        Assert.True(backTriggered);
    }

    [Fact]
    public void PomodoroRoomViewModel_IsBreakPhase_UpdatesCorrectlyOnPhaseTransition()
    {
        var (vm, _) = CreateViewModel(25);

        // Initially in Focus phase
        Assert.False(vm.IsBreakPhase);

        // Skip to Break phase
        vm.Start();
        vm.Skip();

        Assert.True(vm.IsBreakPhase);
        Assert.Contains("Break", vm.PhaseText);
    }

    [Fact]
    public void PomodoroRoomViewModel_TriggerFlashcard_FiresHandlerAndSendsMessengerMessage()
    {
        var messenger = new WeakReferenceMessenger();
        var session = new PomodoroSession { FocusDurationSeconds = 1500, ShortBreakDurationSeconds = 300 };
        var engine = new PomodoroEngine(session);
        var vm = new PomodoroRoomViewModel(engine, messenger);

        FlashcardPromptMessage? receivedViaMessenger = null;
        FlashcardPromptMessage? receivedViaHandler = null;

        messenger.Register<FlashcardPromptMessage>(this, (r, m) => receivedViaMessenger = m);
        vm.OpenFlashcardHandler = msg => receivedViaHandler = msg;

        vm.TriggerFlashcardCommand.Execute(null);

        Assert.NotNull(receivedViaMessenger);
        Assert.NotNull(receivedViaHandler);
        Assert.Equal(receivedViaHandler.CardId, receivedViaMessenger.CardId);
        Assert.Contains(vm.Subject, receivedViaMessenger.Question);
        Assert.Equal(30, receivedViaMessenger.TimeoutSeconds);
    }
}


using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Engines;
using StudyRoom.Application.Helpers;
using StudyRoom.Application.Messages;
using StudyRoom.Application.Services;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class CrossModuleMessengerTests
{
    [Fact]
    public void PomodoroTransitionToBreak_DispatchesPhaseChanged_ExpandsFloatingBubble()
    {
        var messenger = new StrongReferenceMessenger();
        using var pomodoroEngine = new PomodoroEngine(new PomodoroSession
        {
            FocusDurationSeconds = 1500,
            ShortBreakDurationSeconds = 300
        });

        using var pomodoroVm = new PomodoroRoomViewModel(pomodoroEngine, messenger);
        using var bubbleVm = new FloatingBubbleViewModel(new TokenBucketRateLimiter(5, 1), TimeProvider.System, messenger);

        Assert.False(bubbleVm.IsExpanded);

        // Act: Skip Focus phase -> transitions to ShortBreak
        pomodoroVm.Skip();

        // Assert
        Assert.True(bubbleVm.IsVisible);
        Assert.True(bubbleVm.IsExpanded);
        Assert.Contains(bubbleVm.Messages, m => m.Content.Contains("nghỉ ngơi") && m.SenderId == "system");
    }

    [Fact]
    public void PomodoroTransitionBackToFocus_DispatchesPhaseChanged_CollapsesFloatingBubble()
    {
        var messenger = new StrongReferenceMessenger();
        using var pomodoroEngine = new PomodoroEngine(new PomodoroSession
        {
            FocusDurationSeconds = 1500,
            ShortBreakDurationSeconds = 300
        });

        using var pomodoroVm = new PomodoroRoomViewModel(pomodoroEngine, messenger);
        using var bubbleVm = new FloatingBubbleViewModel(new TokenBucketRateLimiter(5, 1), TimeProvider.System, messenger);

        // Skip to Break (expands)
        pomodoroVm.Skip();
        Assert.True(bubbleVm.IsExpanded);

        // Act: Skip Break -> transitions back to Focus
        pomodoroVm.Skip();

        // Assert
        Assert.False(bubbleVm.IsExpanded);
        Assert.Contains(bubbleVm.Messages, m => m.Content.Contains("tập trung mới") && m.SenderId == "system");
    }

    [Fact]
    public void PomodoroPassBaton_DispatchesBatonPassedMessage_AndUpdatesPeerViewModel()
    {
        var messenger = new StrongReferenceMessenger();
        using var hostPomodoroVm = new PomodoroRoomViewModel(new PomodoroEngine(), messenger);
        using var peerPomodoroVm = new PomodoroRoomViewModel(new PomodoroEngine(), messenger);

        // Host passes baton to user-2
        hostPomodoroVm.PassBaton("user-2");

        // Peer should have received the baton update
        var targetMemberInPeer = peerPomodoroVm.Members.FirstOrDefault(m => m.UserId == "user-2");
        Assert.NotNull(targetMemberInPeer);
        Assert.True(targetMemberInPeer.HasBaton);

        var previousHostInPeer = peerPomodoroVm.Members.FirstOrDefault(m => m.UserId == "user-1");
        Assert.NotNull(previousHostInPeer);
        Assert.False(previousHostInPeer.HasBaton);
    }

    [Fact]
    public void PomodoroPresenceChangedMessage_UpdatesMemberList()
    {
        var messenger = new StrongReferenceMessenger();
        using var pomodoroVm = new PomodoroRoomViewModel(new PomodoroEngine(), messenger);

        // Act 1: User-2 goes offline
        messenger.Send(new PresenceChangedMessage("user-2", "Trần Minh", false));
        var user2 = pomodoroVm.Members.FirstOrDefault(m => m.UserId == "user-2");
        Assert.NotNull(user2);
        Assert.False(user2.IsFocusing);
        Assert.Contains("rời phòng", user2.StatusText);

        // Act 2: New user joins
        messenger.Send(new PresenceChangedMessage("user-99", "Nguyễn Văn A", true));
        var user99 = pomodoroVm.Members.FirstOrDefault(m => m.UserId == "user-99");
        Assert.NotNull(user99);
        Assert.True(user99.IsFocusing);
        Assert.Equal("Nguyễn Văn A", user99.Name);
    }

    [Fact]
    public async Task KanbanMoveCardToDone_DispatchesCardMoved_UpdatesDashboardMetrics()
    {
        var messenger = new StrongReferenceMessenger();
        var dashboardService = new MockDashboardService();
        var kanbanService = new KanbanService(new FractionalIndexingHelper());

        using var dashboardVm = new DashboardViewModel(
            dashboardService,
            pomodoroEngine: null,
            kanbanVm: null,
            kanbanService: kanbanService,
            indexingHelper: null,
            messenger: messenger);

        dashboardVm.CompletedTasks = "2/5";

        using var kanbanVm = new KanbanBoardViewModel(kanbanService, null, messenger);
        await kanbanVm.LoadingTask;

        var todoCard = kanbanVm.TodoCards.FirstOrDefault();
        Assert.NotNull(todoCard);

        // Act: Move card to Done
        await kanbanVm.MoveCardAsync(todoCard, KanbanColumnType.Done);

        // Assert: Dashboard received CardMovedMessage and updated completed tasks
        Assert.Equal("3/5", dashboardVm.CompletedTasks);
        Assert.Contains("vừa hoàn thành", dashboardVm.CompletedTasksTrend);
    }

    [Fact]
    public async Task KanbanReceivesExternalCardMovedMessage_SynchronizesCardPosition()
    {
        var messenger = new StrongReferenceMessenger();
        var kanbanService = new KanbanService(new FractionalIndexingHelper());
        using var kanbanVm = new KanbanBoardViewModel(kanbanService, null, messenger);
        await kanbanVm.LoadingTask;

        var targetCard = kanbanVm.TodoCards.FirstOrDefault();
        Assert.NotNull(targetCard);
        string cardId = targetCard.Id.ToString();

        // Act: Simulate external peer moving this card to InProgress
        messenger.Send(new CardMovedMessage(cardId, KanbanColumnType.InProgress, 99.5));

        // Assert: Card is now in InProgressCards and removed from TodoCards
        Assert.DoesNotContain(kanbanVm.TodoCards, c => c.Id == targetCard.Id);
        Assert.Contains(kanbanVm.InProgressCards, c => c.Id == targetCard.Id);
        Assert.Equal(99.5, targetCard.Rank);
    }

    [Fact]
    public void FloatingBubbleSendMessage_BroadcastsChatMessageReceivedMessage()
    {
        var messenger = new StrongReferenceMessenger();
        ChatMessageReceivedMessage? capturedMessage = null;
        messenger.Register<ChatMessageReceivedMessage>(this, (_, m) => capturedMessage = m);

        using var bubbleVm = new FloatingBubbleViewModel(new TokenBucketRateLimiter(5, 1), TimeProvider.System, messenger)
        {
            InputText = "Chào mọi người!"
        };

        // Act
        bubbleVm.SendMessageCommand.Execute(null);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Equal("Chào mọi người!", capturedMessage.Message.Content);
        Assert.True(capturedMessage.Message.IsSelf);

        messenger.UnregisterAll(this);
    }

    [Fact]
    public void FloatingBubbleReceivesExternalChatMessage_AddsWithoutDuplicate()
    {
        var messenger = new StrongReferenceMessenger();
        using var bubbleVm = new FloatingBubbleViewModel(new TokenBucketRateLimiter(5, 1), TimeProvider.System, messenger);

        var msg = new ChatMessage("remote_user", "Bob", "Hello Sally!");

        // Act 1: First reception
        messenger.Send(new ChatMessageReceivedMessage(msg));
        Assert.Single(bubbleVm.Messages);
        Assert.Equal("Hello Sally!", bubbleVm.Messages[0].Content);

        // Act 2: Duplicate message should be ignored
        messenger.Send(new ChatMessageReceivedMessage(msg));
        Assert.Single(bubbleVm.Messages);
    }

    [Fact]
    public void DisposedViewModels_DoNotReceiveSubsequentMessages()
    {
        var messenger = new StrongReferenceMessenger();
        var bubbleVm = new FloatingBubbleViewModel(new TokenBucketRateLimiter(5, 1), TimeProvider.System, messenger);

        // Dispose ViewModel -> unregisters all messenger subscriptions
        bubbleVm.Dispose();

        // Send PhaseChangedMessage
        messenger.Send(new PhaseChangedMessage(PomodoroPhase.ShortBreak, 300));

        // Bubble should NOT have expanded or added messages
        Assert.False(bubbleVm.IsExpanded);
        Assert.Empty(bubbleVm.Messages);
    }
}

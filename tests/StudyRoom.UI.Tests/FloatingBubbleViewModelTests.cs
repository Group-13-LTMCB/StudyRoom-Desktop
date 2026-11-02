using StudyRoom.Application.Engines;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using StudyRoom.UI.Views;
using Xunit;

namespace StudyRoom.UI.Tests;

public class FloatingBubbleViewModelTests
{
    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset initialTime)
        {
            _utcNow = initialTime;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }

    [Fact]
    public void Construction_ShouldInitializeDefaultState()
    {
        var vm = new FloatingBubbleViewModel();

        Assert.Empty(vm.Messages);
        Assert.Equal(0, vm.MessageCount);
        Assert.Empty(vm.InputText);
        Assert.False(vm.IsExpanded);
        Assert.False(vm.IsRateLimited);
        Assert.Null(vm.RateLimitWarning);
        Assert.Equal(TimeSpan.Zero, vm.RetryAfter);
        Assert.True(vm.IsVisible);
        Assert.Equal("user_me", vm.CurrentUserId);
        Assert.Equal("Tôi", vm.CurrentUserName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public void SendMessage_WithEmptyOrWhitespaceInput_ShouldDoNothing(string emptyInput)
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 1, refillRatePerSecond: 1, timeProvider);
        var vm = new FloatingBubbleViewModel(limiter)
        {
            InputText = emptyInput
        };

        vm.SendMessageCommand.Execute(null);

        Assert.Empty(vm.Messages);
        Assert.False(vm.IsRateLimited);

        // Verify the token was not consumed by sending a valid message afterwards
        vm.InputText = "Tin nhắn hợp lệ";
        vm.SendMessageCommand.Execute(null);

        Assert.Single(vm.Messages);
        Assert.Equal("Tin nhắn hợp lệ", vm.Messages[0].Content);
    }

    [Fact]
    public void SendMessage_WithValidText_ConsumesToken_AddsMessage_AndClearsInput()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 3, refillRatePerSecond: 1, timeProvider);
        var vm = new FloatingBubbleViewModel(limiter)
        {
            CurrentUserId = "user_123",
            CurrentUserName = "Alice",
            InputText = "Xin chào cả phòng!"
        };

        vm.SendMessageCommand.Execute(null);

        Assert.Single(vm.Messages);
        var message = vm.Messages[0];
        Assert.Equal("user_123", message.SenderId);
        Assert.Equal("Alice", message.SenderName);
        Assert.Equal("Xin chào cả phòng!", message.Content);
        Assert.True(message.IsSelf);
        Assert.NotEmpty(message.MessageId);
        Assert.Empty(vm.InputText);
        Assert.False(vm.IsRateLimited);
        Assert.Null(vm.RateLimitWarning);
        Assert.Equal(1, vm.MessageCount);
    }

    [Fact]
    public void SendMessage_TrimsWhitespaceFromInput()
    {
        var vm = new FloatingBubbleViewModel
        {
            InputText = "   Nội dung có khoảng cách thừa   "
        };

        vm.SendMessageCommand.Execute(null);

        Assert.Single(vm.Messages);
        Assert.Equal("Nội dung có khoảng cách thừa", vm.Messages[0].Content);
    }

    [Fact]
    public void SendMessage_WhenRateLimitExceeded_RejectsMessage_PreservesInput_AndSetsWarning()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 2, refillRatePerSecond: 1, timeProvider);
        var vm = new FloatingBubbleViewModel(limiter);

        // Send 2 messages to exhaust tokens
        vm.InputText = "Message 1";
        vm.SendMessageCommand.Execute(null);
        vm.InputText = "Message 2";
        vm.SendMessageCommand.Execute(null);

        Assert.Equal(2, vm.Messages.Count);
        Assert.False(vm.IsRateLimited);

        // Attempt 3rd message
        vm.InputText = "Message 3 - Spam";
        vm.SendMessageCommand.Execute(null);

        // Rejection verification
        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal("Message 3 - Spam", vm.InputText); // Input preserved
        Assert.True(vm.IsRateLimited);
        Assert.NotNull(vm.RateLimitWarning);
        Assert.Contains("quá nhanh", vm.RateLimitWarning);
        Assert.True(vm.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void SendMessage_AfterCooldown_AllowsNewMessagesAndClearsWarning()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 1, refillRatePerSecond: 1, timeProvider);
        var vm = new FloatingBubbleViewModel(limiter);

        // First message consumes the only token
        vm.InputText = "First message";
        vm.SendMessageCommand.Execute(null);
        Assert.Single(vm.Messages);

        // Second message blocked
        vm.InputText = "Blocked message";
        vm.SendMessageCommand.Execute(null);
        Assert.Single(vm.Messages);
        Assert.True(vm.IsRateLimited);
        Assert.NotNull(vm.RateLimitWarning);

        // Advance time by 1.5 seconds -> token refills
        timeProvider.Advance(TimeSpan.FromSeconds(1.5));

        // Retry sending the preserved message
        vm.SendMessageCommand.Execute(null);

        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal("Blocked message", vm.Messages[1].Content);
        Assert.Empty(vm.InputText);
        Assert.False(vm.IsRateLimited);
        Assert.Null(vm.RateLimitWarning);
        Assert.Equal(TimeSpan.Zero, vm.RetryAfter);
    }

    [Fact]
    public void ToggleExpand_InvertsIsExpandedState_AndRaisesRequestFocusInputWhenExpanding()
    {
        var vm = new FloatingBubbleViewModel();
        bool focusRequested = false;
        vm.RequestFocusInput += () => focusRequested = true;

        Assert.False(vm.IsExpanded);

        vm.ToggleExpandCommand.Execute(null);
        Assert.True(vm.IsExpanded);
        Assert.True(focusRequested);

        focusRequested = false;
        vm.ToggleExpandCommand.Execute(null);
        Assert.False(vm.IsExpanded);
        Assert.False(focusRequested);
    }

    [Fact]
    public void CloseCommand_SetsIsVisibleFalse_AndRaisesRequestClose()
    {
        var vm = new FloatingBubbleViewModel();
        bool closeRequested = false;
        vm.RequestClose += () => closeRequested = true;

        Assert.True(vm.IsVisible);

        vm.CloseCommand.Execute(null);

        Assert.False(vm.IsVisible);
        Assert.True(closeRequested);
    }

    [Fact]
    public void ToggleVisibility_InvertsIsVisible_AndRaisesRequestToggleVisibility()
    {
        var vm = new FloatingBubbleViewModel();
        bool toggleRequested = false;
        vm.RequestToggleVisibility += () => toggleRequested = true;

        Assert.True(vm.IsVisible);

        vm.ToggleVisibilityCommand.Execute(null);
        Assert.False(vm.IsVisible);
        Assert.True(toggleRequested);

        toggleRequested = false;
        vm.ToggleVisibilityCommand.Execute(null);
        Assert.True(vm.IsVisible);
        Assert.True(toggleRequested);
    }

    [Fact]
    public void ClearWarning_ResetsRateLimitState()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 1, refillRatePerSecond: 1, timeProvider);
        var vm = new FloatingBubbleViewModel(limiter);

        vm.InputText = "Msg 1";
        vm.SendMessageCommand.Execute(null);
        vm.InputText = "Msg 2";
        vm.SendMessageCommand.Execute(null);
        Assert.True(vm.IsRateLimited);

        vm.ClearWarning();

        Assert.False(vm.IsRateLimited);
        Assert.Null(vm.RateLimitWarning);
        Assert.Equal(TimeSpan.Zero, vm.RetryAfter);
    }

    [Fact]
    public void ReceiveMessage_AddsExternalMessageToCollection()
    {
        var vm = new FloatingBubbleViewModel();
        var incoming = new ChatMessage("user_bob", "Bob", "Hello Sally!", isSelf: false);

        vm.ReceiveMessage(incoming);

        Assert.Single(vm.Messages);
        Assert.Equal("Bob", vm.Messages[0].SenderName);
        Assert.Equal("Hello Sally!", vm.Messages[0].Content);
        Assert.False(vm.Messages[0].IsSelf);
    }

    [Fact]
    public void ReceiveMessage_WithNull_ThrowsArgumentNullException()
    {
        var vm = new FloatingBubbleViewModel();
        Assert.Throws<ArgumentNullException>(() => vm.ReceiveMessage(null!));
    }

    [Fact]
    public void Messages_CollectionChanges_RaisePropertyChangedForMessageCountAndHasMessages()
    {
        var vm = new FloatingBubbleViewModel();
        var changedProps = new List<string>();
        vm.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProps.Add(e.PropertyName);
            }
        };

        Assert.False(vm.HasMessages);
        Assert.Equal(0, vm.MessageCount);

        vm.Messages.Add(new ChatMessage("user1", "User 1", "Hello"));

        Assert.True(vm.HasMessages);
        Assert.Equal(1, vm.MessageCount);
        Assert.Contains(nameof(FloatingBubbleViewModel.MessageCount), changedProps);
        Assert.Contains(nameof(FloatingBubbleViewModel.HasMessages), changedProps);

        changedProps.Clear();
        vm.Messages.Clear();

        Assert.False(vm.HasMessages);
        Assert.Equal(0, vm.MessageCount);
        Assert.Contains(nameof(FloatingBubbleViewModel.MessageCount), changedProps);
        Assert.Contains(nameof(FloatingBubbleViewModel.HasMessages), changedProps);
    }

    [Fact]
    public async Task ReceiveMessage_FromBackgroundThread_AddsMessageSuccessfully()
    {
        var vm = new FloatingBubbleViewModel();
        var incoming = new ChatMessage("user_async", "AsyncUser", "Message from async task");

        await Task.Run(() => vm.ReceiveMessage(incoming));

        Assert.Single(vm.Messages);
        Assert.Equal("AsyncUser", vm.Messages[0].SenderName);
        Assert.True(vm.HasMessages);
    }

    [Fact]
    public void FloatingBubbleView_CanBeConstructedOnStaThread()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var vm = new FloatingBubbleViewModel();
                var view = new FloatingBubbleView(vm);
                Assert.NotNull(view);
                Assert.Same(vm, view.DataContext);
                Assert.Same(vm, view.ViewModel);
                Assert.True(view.Topmost);
                Assert.True(view.AllowsTransparency);
                Assert.Equal(System.Windows.WindowStyle.None, view.WindowStyle);

                // Test RequestToggleVisibility handler
                vm.ToggleVisibilityCommand.Execute(null);
                Assert.False(vm.IsVisible);

                vm.CloseCommand.Execute(null);
                Assert.False(vm.IsVisible);

                vm.Dispose();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
    }

    [Fact]
    public void MainWindow_ToggleFloatingBubble_TogglesBubbleVisibility()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var bubbleVm = new FloatingBubbleViewModel();
                var bubbleView = new FloatingBubbleView(bubbleVm);
                var dashboardVm = new DashboardViewModel(new StudyRoom.Application.Services.MockDashboardService());
                var mainWindow = new MainWindow(dashboardVm, null, bubbleView);

                Assert.True(bubbleVm.IsVisible);

                mainWindow.ToggleFloatingBubble();
                Assert.False(bubbleVm.IsVisible);

                mainWindow.ToggleFloatingBubble();
                Assert.True(bubbleVm.IsVisible);

                bubbleView.ForceClose();
                bubbleVm.Dispose();
                dashboardVm.Dispose();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
    }
}

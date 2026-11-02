using CommunityToolkit.Mvvm.Messaging;
using StudyRoom.Application.Messages;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class FlashcardViewModelTests
{
    [Fact]
    public void Construction_SetsDefaultProperties()
    {
        using var vm = new FlashcardViewModel();

        Assert.Empty(vm.CardId);
        Assert.Empty(vm.Question);
        Assert.Empty(vm.Answer);
        Assert.False(vm.IsFlipped);
        Assert.False(vm.IsAnswered);
        Assert.Null(vm.IsCorrect);
        Assert.False(vm.IsOpen);
        Assert.False(vm.IsTimerRunning);
        Assert.Equal(30, vm.TotalTimeoutSeconds);
        Assert.Equal(30, vm.RemainingSeconds);
    }

    [Fact]
    public void LoadCard_SetsPropertiesAndStartsTimer()
    {
        using var vm = new FlashcardViewModel();

        vm.LoadCard("card_001", "What is DPAPI?", "Data Protection API", 20);

        Assert.Equal("card_001", vm.CardId);
        Assert.Equal("What is DPAPI?", vm.Question);
        Assert.Equal("Data Protection API", vm.Answer);
        Assert.Equal(20, vm.TotalTimeoutSeconds);
        Assert.Equal(20, vm.RemainingSeconds);
        Assert.Equal(100.0, vm.TimeProgressPercent);
        Assert.False(vm.IsFlipped);
        Assert.False(vm.IsAnswered);
        Assert.Null(vm.IsCorrect);
        Assert.True(vm.IsOpen);
        Assert.True(vm.IsTimerRunning);
    }

    [Fact]
    public void FlipCard_TogglesIsFlippedAndFiresEvent()
    {
        using var vm = new FlashcardViewModel();
        vm.LoadCard("c1", "Q1", "A1");

        bool? eventFlippedValue = null;
        vm.RequestFlipAnimation += val => eventFlippedValue = val;

        vm.FlipCard();
        Assert.True(vm.IsFlipped);
        Assert.True(eventFlippedValue);

        vm.FlipCard();
        Assert.False(vm.IsFlipped);
        Assert.False(eventFlippedValue);
    }

    [Fact]
    public void SubmitAnswer_Correct_UpdatesStatusAndRevealsAnswer()
    {
        using var vm = new FlashcardViewModel();
        vm.LoadCard("c2", "What is MVVM?", "Model-View-ViewModel", 30);

        string? submittedCardId = null;
        bool? submittedResult = null;
        vm.AnswerSubmitted += (id, res) =>
        {
            submittedCardId = id;
            submittedResult = res;
        };

        vm.SubmitAnswer(true);

        Assert.True(vm.IsAnswered);
        Assert.True(vm.IsCorrect);
        Assert.True(vm.IsFlipped);
        Assert.False(vm.IsTimerRunning);
        Assert.Contains("Tuyệt vời", vm.StatusMessage);
        Assert.Equal("c2", submittedCardId);
        Assert.True(submittedResult);
    }

    [Fact]
    public void SubmitAnswer_Incorrect_UpdatesStatusAndRevealsAnswer()
    {
        using var vm = new FlashcardViewModel();
        vm.LoadCard("c3", "Q", "A", 30);

        vm.SubmitAnswer(false);

        Assert.True(vm.IsAnswered);
        Assert.False(vm.IsCorrect);
        Assert.True(vm.IsFlipped);
        Assert.Contains("ôn tập lại", vm.StatusMessage);
    }

    [Fact]
    public void Receive_FlashcardPromptMessage_LoadsCardViaMessenger()
    {
        var messenger = new WeakReferenceMessenger();
        using var vm = new FlashcardViewModel(messenger);

        messenger.Send(new FlashcardPromptMessage("msg_card_1", "Prompt Question", "Prompt Answer", 15));

        Assert.Equal("msg_card_1", vm.CardId);
        Assert.Equal("Prompt Question", vm.Question);
        Assert.Equal("Prompt Answer", vm.Answer);
        Assert.Equal(15, vm.TotalTimeoutSeconds);
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public void CloseDialog_StopsTimerAndFiresCloseEvent()
    {
        using var vm = new FlashcardViewModel();
        vm.LoadCard("c4", "Q", "A", 30);

        bool closeFired = false;
        vm.RequestClose += () => closeFired = true;

        vm.CloseDialog();

        Assert.False(vm.IsOpen);
        Assert.False(vm.IsTimerRunning);
        Assert.True(closeFired);
    }

    [Fact]
    public void LoadCard_FiresRequestResetCardAndRequestShowEvents()
    {
        using var vm = new FlashcardViewModel();

        bool resetFired = false;
        bool showFired = false;
        vm.RequestResetCard += () => resetFired = true;
        vm.RequestShow += () => showFired = true;

        vm.LoadCard("c5", "Question", "Answer", 25);

        Assert.True(resetFired);
        Assert.True(showFired);
    }

    [Fact]
    public void FlashcardDialog_FlipAndReset_WorksInStaThread()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var vm = new FlashcardViewModel();
                var dialog = new StudyRoom.UI.Views.FlashcardDialog(vm);

                vm.LoadCard("c6", "Test Q", "Test A", 10);
                Assert.Equal("Test Q", vm.Question);

                // Trigger flip
                vm.FlipCard();
                Assert.True(vm.IsFlipped);

                // Trigger reset
                vm.LoadCard("c7", "Next Q", "Next A", 10);
                Assert.False(vm.IsFlipped);

                dialog.Close();
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

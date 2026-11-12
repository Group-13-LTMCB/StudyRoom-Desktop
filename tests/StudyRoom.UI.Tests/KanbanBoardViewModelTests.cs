using System.Windows;
using StudyRoom.Application.Helpers;
using StudyRoom.Application.Services;
using StudyRoom.Core.Enums;
using StudyRoom.UI.ViewModels;
using StudyRoom.UI.Views;
using Xunit;

namespace StudyRoom.UI.Tests;

public class KanbanBoardViewModelTests
{
    private static KanbanBoardViewModel CreateViewModel()
    {
        var indexing = new FractionalIndexingHelper();
        var service = new KanbanService(indexing);
        return new KanbanBoardViewModel(service, indexing);
    }

    [Fact]
    public async Task Construction_LoadsDefaultCardsIntoThreeColumns()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        Assert.NotEmpty(vm.TodoCards);
        Assert.NotEmpty(vm.InProgressCards);
        Assert.NotEmpty(vm.DoneCards);

        Assert.Equal(vm.TodoCards.Count, vm.TodoCount);
        Assert.Equal(vm.InProgressCards.Count, vm.InProgressCount);
        Assert.Equal(vm.DoneCards.Count, vm.DoneCount);

        Assert.True(vm.TodoCards.All(c => c.Column == KanbanColumnType.Todo));
        Assert.True(vm.InProgressCards.All(c => c.Column == KanbanColumnType.InProgress));
        Assert.True(vm.DoneCards.All(c => c.Column == KanbanColumnType.Done));
    }

    [Fact]
    public async Task AddCard_WithValidTitle_AddsCardToSelectedColumn()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        int initialTodoCount = vm.TodoCount;

        vm.OpenAddCardDialog(KanbanColumnType.Todo);
        vm.NewCardTitle = "Làm bài tập Mạng máy tính";
        vm.NewCardDescription = "Cấu hình router Cisco Packet Tracer";

        await vm.SaveNewCardAsync();

        Assert.False(vm.IsAddCardDialogOpen);
        Assert.Null(vm.AddCardValidationError);
        Assert.Equal(initialTodoCount + 1, vm.TodoCount);
        Assert.Contains(vm.TodoCards, c => c.Title == "Làm bài tập Mạng máy tính");
    }

    [Fact]
    public async Task AddCard_WithEmptyTitle_SetsValidationError()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        vm.OpenAddCardDialog(KanbanColumnType.InProgress);
        vm.NewCardTitle = "   ";

        await vm.SaveNewCardAsync();

        Assert.True(vm.IsAddCardDialogOpen);
        Assert.NotNull(vm.AddCardValidationError);
    }

    [Fact]
    public async Task MoveCardRight_AdvancesCardThroughColumns()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        var card = vm.TodoCards.First();
        int initialTodo = vm.TodoCount;
        int initialInProgress = vm.InProgressCount;

        // Move right: Todo -> InProgress
        await vm.MoveCardRightAsync(card);

        Assert.Equal(KanbanColumnType.InProgress, card.Column);
        Assert.Equal(initialTodo - 1, vm.TodoCount);
        Assert.Equal(initialInProgress + 1, vm.InProgressCount);
        Assert.Contains(vm.InProgressCards, c => c.Id == card.Id);

        // Move right: InProgress -> Done
        int initialDone = vm.DoneCount;
        await vm.MoveCardRightAsync(card);

        Assert.Equal(KanbanColumnType.Done, card.Column);
        Assert.Equal(initialDone + 1, vm.DoneCount);
        Assert.Contains(vm.DoneCards, c => c.Id == card.Id);
    }

    [Fact]
    public async Task MoveCardLeft_RegressesCardThroughColumns()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        var card = vm.DoneCards.First();
        int initialDone = vm.DoneCount;
        int initialInProgress = vm.InProgressCount;

        // Move left: Done -> InProgress
        await vm.MoveCardLeftAsync(card);

        Assert.Equal(KanbanColumnType.InProgress, card.Column);
        Assert.Equal(initialDone - 1, vm.DoneCount);
        Assert.Equal(initialInProgress + 1, vm.InProgressCount);
        Assert.Contains(vm.InProgressCards, c => c.Id == card.Id);

        // Move left: InProgress -> Todo
        int initialTodo = vm.TodoCount;
        await vm.MoveCardLeftAsync(card);

        Assert.Equal(KanbanColumnType.Todo, card.Column);
        Assert.Equal(initialTodo + 1, vm.TodoCount);
        Assert.Contains(vm.TodoCards, c => c.Id == card.Id);
    }

    [Fact]
    public async Task MoveCard_BoundsCheck_PreventsInvalidMoves()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        var todoCard = vm.TodoCards.First();
        Assert.False(todoCard.CanMoveLeft);
        await vm.MoveCardLeftAsync(todoCard);
        Assert.Equal(KanbanColumnType.Todo, todoCard.Column);

        var doneCard = vm.DoneCards.First();
        Assert.False(doneCard.CanMoveRight);
        await vm.MoveCardRightAsync(doneCard);
        Assert.Equal(KanbanColumnType.Done, doneCard.Column);
    }

    [Fact]
    public async Task DeleteCard_RemovesCardAndUpdatesCounts()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        var card = vm.TodoCards.First();
        int initialTodo = vm.TodoCount;

        await vm.DeleteCardAsync(card);

        Assert.Equal(initialTodo - 1, vm.TodoCount);
        Assert.DoesNotContain(vm.TodoCards, c => c.Id == card.Id);
    }

    [Fact]
    public async Task SearchFilter_FiltersByTitleAndRestoresOnClear()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        int totalBefore = vm.TodoCards.Count + vm.InProgressCards.Count + vm.DoneCards.Count;

        // Filter for specific card
        vm.SearchText = "Giải tích";
        int totalFiltered = vm.TodoCards.Count + vm.InProgressCards.Count + vm.DoneCards.Count;
        Assert.True(totalFiltered > 0);
        Assert.True(totalFiltered < totalBefore);

        // Clear filter
        vm.SearchText = string.Empty;
        int totalAfterClear = vm.TodoCards.Count + vm.InProgressCards.Count + vm.DoneCards.Count;
        Assert.Equal(totalBefore, totalAfterClear);
    }

    [Fact]
    public void BackToDashboard_RaisesEvent()
    {
        var vm = CreateViewModel();
        bool eventFired = false;
        vm.RequestBackToDashboard += () => eventFired = true;

        vm.BackToDashboard();

        Assert.True(eventFired);
    }

    [Fact]
    public void DashboardViewModel_NavigatesToKanbanAndBack()
    {
        var service = new MockDashboardService();
        var dashboardVm = new DashboardViewModel(service);

        // Navigate to Kanban (index = 2)
        dashboardVm.NavigateTo(2);
        Assert.Equal(2, dashboardVm.SelectedNavIndex);

        // Click Back to Dashboard on Kanban
        dashboardVm.KanbanVm.BackToDashboard();
        Assert.Equal(0, dashboardVm.SelectedNavIndex);
    }

    [Fact]
    public void DefaultGroupId_IsValidGuid()
    {
        Assert.True(Guid.TryParse(KanbanBoardViewModel.DefaultGroupId, out var guid));
        Assert.NotEqual(Guid.Empty, guid);
    }

    [Fact]
    public void OpenAddCardDialog_WithoutParam_DefaultsToTodoColumn()
    {
        var vm = CreateViewModel();
        vm.OpenAddCardDialog(KanbanColumnType.Done);
        Assert.Equal(KanbanColumnType.Done, vm.SelectedNewCardColumn);

        vm.OpenAddCardDialog();
        Assert.Equal(KanbanColumnType.Todo, vm.SelectedNewCardColumn);
        Assert.True(vm.IsColumnTodo);
    }

    [Fact]
    public async Task MoveCardAsync_WithTargetIndexZero_CalculatesFractionalRank()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        var card = vm.TodoCards.First();
        // Move to Done column at index 0 (before DoneCards[0] which has rank 1.0)
        await vm.MoveCardAsync(card, KanbanColumnType.Done, targetIndex: 0);

        Assert.Equal(KanbanColumnType.Done, card.Column);
        Assert.Equal(0.5, card.Rank);
        Assert.Equal(card.Id, vm.DoneCards.First().Id);
    }

    [Fact]
    public async Task MoveCardRelativeToAsync_ReordersCardsWithinSameColumn()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        // TodoCards initially has [Card0 (rank 1.0), Card1 (rank 2.0)]
        var card0 = vm.TodoCards[0];
        var card1 = vm.TodoCards[1];

        // Move card0 after card1
        await vm.MoveCardRelativeToAsync(card0, KanbanColumnType.Todo, card1, dropAfter: true);

        // Card0 should now have rank > card1.Rank
        Assert.True(card0.Rank > card1.Rank);
        Assert.Equal(card1.Id, vm.TodoCards[0].Id);
        Assert.Equal(card0.Id, vm.TodoCards[1].Id);
    }

    [Fact]
    public async Task MoveCardAsync_WhenRebalanceOccurs_SynchronizesAllRanksInViewModel()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        var card = vm.TodoCards.First();
        await vm.MoveCardAsync(card, KanbanColumnType.Done, targetIndex: -1);

        var inProgressCard = vm.InProgressCards.First();
        await vm.MoveCardAsync(inProgressCard, KanbanColumnType.Done, targetIndex: 1);

        var doneRanks = vm.DoneCards.Select(c => c.Rank).ToList();
        Assert.Equal(doneRanks.Count, doneRanks.Distinct().Count());
        for (int i = 0; i < doneRanks.Count - 1; i++)
        {
            Assert.True(doneRanks[i] < doneRanks[i + 1]);
        }
    }

    [Fact]
    public async Task SearchFilter_UpdatesCountersToDisplayCount()
    {
        var vm = CreateViewModel();
        await vm.LoadCardsAsync();

        vm.SearchText = "Giải tích";

        Assert.Equal(vm.TodoCards.Count, vm.TodoCount);
        Assert.Equal(vm.InProgressCards.Count, vm.InProgressCount);
        Assert.Equal(vm.DoneCards.Count, vm.DoneCount);
    }

    [Fact]
    public void DashboardView_WhenNavigatingToKanban_BindsVisibilityCorrectly()
    {
        var service = new MockDashboardService();
        var dashboardVm = new DashboardViewModel(service);

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var view = new DashboardView { DataContext = dashboardVm };
                view.Measure(new Size(1280, 800));
                view.Arrange(new Rect(0, 0, 1280, 800));
                view.UpdateLayout();

                // By default SelectedNavIndex is 0
                Assert.Equal(0, dashboardVm.SelectedNavIndex);

                // Switch to Kanban (index = 2)
                dashboardVm.NavigateTo(2);
                view.UpdateLayout();

                // Find KanbanBoardView in visual tree
                var kanbanView = FindVisualChild<KanbanBoardView>(view);
                Assert.NotNull(kanbanView);

                // Verify parent container grid is Visible
                var parentGrid = (FrameworkElement)kanbanView.Parent;
                Assert.Equal(Visibility.Visible, parentGrid.Visibility);

                // Switch back to 0
                dashboardVm.NavigateTo(0);
                view.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, parentGrid.Visibility);
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

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
    }

    [Fact]
    public void KanbanCardControl_CanInstantiateAndRender()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var card = new StudyRoom.Core.Entities.KanbanCard
                {
                    Id = Guid.NewGuid(),
                    Title = "Test card",
                    Description = "Desc",
                    Column = KanbanColumnType.InProgress,
                    Rank = 1.0
                };
                var itemVm = new KanbanCardItemViewModel(card);
                var control = new StudyRoom.UI.Views.Components.KanbanCardControl { DataContext = itemVm };
                control.Measure(new Size(300, 200));
                control.Arrange(new Rect(0, 0, 300, 200));
                control.UpdateLayout();

                Assert.Equal("Test card", itemVm.Title);
                Assert.True(itemVm.CanMoveLeft);
                Assert.True(itemVm.CanMoveRight);
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

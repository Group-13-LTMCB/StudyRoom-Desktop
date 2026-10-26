using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Application.Helpers;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Services;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.UI.ViewModels;

public partial class KanbanBoardViewModel : ObservableObject
{
    private readonly IKanbanService _kanbanService;
    private readonly IFractionalIndexingHelper _indexingHelper;
    private readonly List<KanbanCardItemViewModel> _allCards = [];

    public const string DefaultGroupId = "00000000-0000-0000-0000-000000000001";

    public Task LoadingTask { get; private set; }

    [ObservableProperty]
    private string _currentGroupId = DefaultGroupId;

    [ObservableProperty]
    private string _boardTitle = "Bảng Nhiệm Vụ & Kanban";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _todoCount;

    [ObservableProperty]
    private int _inProgressCount;

    [ObservableProperty]
    private int _doneCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // Add card dialog properties
    [ObservableProperty]
    private bool _isAddCardDialogOpen;

    [ObservableProperty]
    private string _newCardTitle = string.Empty;

    [ObservableProperty]
    private string _newCardDescription = string.Empty;

    [ObservableProperty]
    private KanbanColumnType _selectedNewCardColumn = KanbanColumnType.Todo;

    [ObservableProperty]
    private string? _addCardValidationError;

    public bool IsColumnTodo
    {
        get => SelectedNewCardColumn == KanbanColumnType.Todo;
        set
        {
            if (value) SelectedNewCardColumn = KanbanColumnType.Todo;
        }
    }

    public bool IsColumnInProgress
    {
        get => SelectedNewCardColumn == KanbanColumnType.InProgress;
        set
        {
            if (value) SelectedNewCardColumn = KanbanColumnType.InProgress;
        }
    }

    public bool IsColumnDone
    {
        get => SelectedNewCardColumn == KanbanColumnType.Done;
        set
        {
            if (value) SelectedNewCardColumn = KanbanColumnType.Done;
        }
    }

    partial void OnSelectedNewCardColumnChanged(KanbanColumnType value)
    {
        OnPropertyChanged(nameof(IsColumnTodo));
        OnPropertyChanged(nameof(IsColumnInProgress));
        OnPropertyChanged(nameof(IsColumnDone));
    }

    public ObservableCollection<KanbanCardItemViewModel> TodoCards { get; } = [];
    public ObservableCollection<KanbanCardItemViewModel> InProgressCards { get; } = [];
    public ObservableCollection<KanbanCardItemViewModel> DoneCards { get; } = [];

    public event Action? RequestBackToDashboard;

    public KanbanBoardViewModel(
        IKanbanService? kanbanService = null,
        IFractionalIndexingHelper? indexingHelper = null)
    {
        _indexingHelper = indexingHelper ?? new FractionalIndexingHelper();
        _kanbanService = kanbanService ?? new KanbanService(_indexingHelper);

        LoadingTask = LoadCardsAsync();
    }

    [RelayCommand]
    public async Task LoadCardsAsync(string? groupId = null)
    {
        IsLoading = true;
        ErrorMessage = null;
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            CurrentGroupId = groupId;
        }

        try
        {
            var cards = await _kanbanService.GetCardsAsync(CurrentGroupId);
            _allCards.Clear();
            foreach (var card in cards)
            {
                _allCards.Add(CreateItemViewModel(card));
            }
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi tải bảng Kanban: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = SearchText?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrEmpty(query)
            ? _allCards
            : _allCards.Where(c =>
                c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (c.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                c.TagText.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        TodoCards.Clear();
        InProgressCards.Clear();
        DoneCards.Clear();

        foreach (var card in filtered.OrderBy(c => c.Rank))
        {
            switch (card.Column)
            {
                case KanbanColumnType.Todo:
                    TodoCards.Add(card);
                    break;
                case KanbanColumnType.InProgress:
                    InProgressCards.Add(card);
                    break;
                case KanbanColumnType.Done:
                    DoneCards.Add(card);
                    break;
            }
        }

        TodoCount = TodoCards.Count;
        InProgressCount = InProgressCards.Count;
        DoneCount = DoneCards.Count;
    }

    public async Task MoveCardAsync(KanbanCardItemViewModel? card, KanbanColumnType targetColumn, int targetIndex = -1)
    {
        if (card == null) return;

        var targetColCards = _allCards
            .Where(c => c.Column == targetColumn && c.Id != card.Id)
            .OrderBy(c => c.Rank)
            .ToList();

        double? prevRank = null;
        double? nextRank = null;

        if (targetIndex < 0 || targetIndex >= targetColCards.Count)
        {
            prevRank = targetColCards.Count > 0 ? targetColCards[^1].Rank : null;
            nextRank = null;
        }
        else if (targetIndex == 0)
        {
            prevRank = null;
            nextRank = targetColCards[0].Rank;
        }
        else
        {
            prevRank = targetColCards[targetIndex - 1].Rank;
            nextRank = targetColCards[targetIndex].Rank;
        }

        try
        {
            var updated = await _kanbanService.MoveCardAsync(card.Id, targetColumn, prevRank, nextRank);
            card.Column = updated.Column;
            card.Rank = updated.Rank;

            // Sync all card ranks in case rebalancing occurred
            var freshCards = await _kanbanService.GetCardsAsync(CurrentGroupId);
            var freshDict = freshCards.ToDictionary(c => c.Id);
            foreach (var item in _allCards)
            {
                if (freshDict.TryGetValue(item.Id, out var fresh))
                {
                    item.Rank = fresh.Rank;
                    item.Column = fresh.Column;
                }
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi khi di chuyển thẻ: {ex.Message}";
        }
    }

    public async Task MoveCardRelativeToAsync(
        KanbanCardItemViewModel? card,
        KanbanColumnType targetColumn,
        KanbanCardItemViewModel? targetCard,
        bool dropAfter)
    {
        if (card == null) return;

        if (targetCard == null || card.Id == targetCard.Id)
        {
            if (card.Column != targetColumn)
            {
                await MoveCardAsync(card, targetColumn);
            }
            return;
        }

        var targetColCards = _allCards
            .Where(c => c.Column == targetColumn && c.Id != card.Id)
            .OrderBy(c => c.Rank)
            .ToList();

        int targetPos = targetColCards.FindIndex(c => c.Id == targetCard.Id);
        if (targetPos < 0)
        {
            await MoveCardAsync(card, targetColumn);
            return;
        }

        int targetIndex = dropAfter ? targetPos + 1 : targetPos;
        await MoveCardAsync(card, targetColumn, targetIndex);
    }

    [RelayCommand]
    public async Task MoveCardLeftAsync(KanbanCardItemViewModel? card)
    {
        if (card == null || !card.CanMoveLeft) return;
        var targetCol = card.Column == KanbanColumnType.Done ? KanbanColumnType.InProgress : KanbanColumnType.Todo;
        await MoveCardAsync(card, targetCol);
    }

    [RelayCommand]
    public async Task MoveCardRightAsync(KanbanCardItemViewModel? card)
    {
        if (card == null || !card.CanMoveRight) return;
        var targetCol = card.Column == KanbanColumnType.Todo ? KanbanColumnType.InProgress : KanbanColumnType.Done;
        await MoveCardAsync(card, targetCol);
    }

    [RelayCommand]
    public async Task DeleteCardAsync(KanbanCardItemViewModel? card)
    {
        if (card == null) return;
        try
        {
            await _kanbanService.DeleteCardAsync(card.Id);
            _allCards.Remove(card);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi khi xóa thẻ: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenAddCardDialog(object? columnParam = null)
    {
        if (columnParam is KanbanColumnType col)
        {
            SelectedNewCardColumn = col;
        }
        else if (columnParam is string str && Enum.TryParse<KanbanColumnType>(str, out var parsedCol))
        {
            SelectedNewCardColumn = parsedCol;
        }
        else if (columnParam is int intCol && Enum.IsDefined(typeof(KanbanColumnType), intCol))
        {
            SelectedNewCardColumn = (KanbanColumnType)intCol;
        }
        else
        {
            SelectedNewCardColumn = KanbanColumnType.Todo;
        }

        NewCardTitle = string.Empty;
        NewCardDescription = string.Empty;
        AddCardValidationError = null;
        IsAddCardDialogOpen = true;
    }

    [RelayCommand]
    public void CloseAddCardDialog()
    {
        IsAddCardDialogOpen = false;
        AddCardValidationError = null;
    }

    [RelayCommand]
    public async Task SaveNewCardAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCardTitle))
        {
            AddCardValidationError = "Vui lòng nhập tiêu đề nhiệm vụ.";
            return;
        }

        try
        {
            var created = await _kanbanService.AddCardAsync(
                CurrentGroupId,
                NewCardTitle.Trim(),
                string.IsNullOrWhiteSpace(NewCardDescription) ? null : NewCardDescription.Trim(),
                SelectedNewCardColumn);

            var itemVm = CreateItemViewModel(created);
            _allCards.Add(itemVm);
            ApplyFilter();

            IsAddCardDialogOpen = false;
            NewCardTitle = string.Empty;
            NewCardDescription = string.Empty;
            AddCardValidationError = null;
        }
        catch (Exception ex)
        {
            AddCardValidationError = $"Lỗi khi thêm nhiệm vụ: {ex.Message}";
        }
    }

    [RelayCommand]
    public void BackToDashboard()
    {
        RequestBackToDashboard?.Invoke();
    }

    private KanbanCardItemViewModel CreateItemViewModel(KanbanCard card)
    {
        return new KanbanCardItemViewModel(
            card,
            onMoveLeft: MoveCardLeftAsync,
            onMoveRight: MoveCardRightAsync,
            onDelete: DeleteCardAsync);
    }
}

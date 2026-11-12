using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;

namespace StudyRoom.UI.ViewModels;

public partial class KanbanCardItemViewModel : ObservableObject
{
    private readonly Func<KanbanCardItemViewModel, Task>? _onMoveLeft;
    private readonly Func<KanbanCardItemViewModel, Task>? _onMoveRight;
    private readonly Func<KanbanCardItemViewModel, Task>? _onDelete;

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private KanbanColumnType _column;

    [ObservableProperty]
    private double _rank;

    [ObservableProperty]
    private string _priorityColor = "#3B82F6";

    [ObservableProperty]
    private string _tagText = "Học tập";

    public bool CanMoveLeft => Column != KanbanColumnType.Todo;
    public bool CanMoveRight => Column != KanbanColumnType.Done;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    partial void OnColumnChanged(KanbanColumnType value)
    {
        OnPropertyChanged(nameof(CanMoveLeft));
        OnPropertyChanged(nameof(CanMoveRight));
    }

    partial void OnDescriptionChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDescription));
    }

    public KanbanCardItemViewModel(
        KanbanCard card,
        Func<KanbanCardItemViewModel, Task>? onMoveLeft = null,
        Func<KanbanCardItemViewModel, Task>? onMoveRight = null,
        Func<KanbanCardItemViewModel, Task>? onDelete = null)
    {
        _id = card.Id;
        _title = card.Title;
        _description = card.Description;
        _column = card.Column;
        _rank = card.Rank;
        _onMoveLeft = onMoveLeft;
        _onMoveRight = onMoveRight;
        _onDelete = onDelete;

        AssignTagAndColor(card.Title);
    }

    private void AssignTagAndColor(string title)
    {
        if (title.Contains("Giải tích", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Toán", StringComparison.OrdinalIgnoreCase))
        {
            TagText = "Toán cao cấp";
            PriorityColor = "#3B82F6"; // Blue
        }
        else if (title.Contains("Vật lý", StringComparison.OrdinalIgnoreCase))
        {
            TagText = "Vật lý";
            PriorityColor = "#8B5CF6"; // Purple
        }
        else if (title.Contains("Tiếng Anh", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("IELTS", StringComparison.OrdinalIgnoreCase))
        {
            TagText = "Ngoại ngữ";
            PriorityColor = "#10B981"; // Green
        }
        else if (title.Contains("Cấu trúc", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("Dữ liệu", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("Lập trình", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("IT", StringComparison.OrdinalIgnoreCase))
        {
            TagText = "CNTT";
            PriorityColor = "#F59E0B"; // Amber
        }
        else if (title.Contains("Cơ sở dữ liệu", StringComparison.OrdinalIgnoreCase) ||
                 title.Contains("SQL", StringComparison.OrdinalIgnoreCase))
        {
            TagText = "Hệ thống TT";
            PriorityColor = "#EC4899"; // Pink
        }
        else
        {
            TagText = "Nhiệm vụ";
            PriorityColor = "#6366F1"; // Indigo
        }
    }

    [RelayCommand]
    public async Task MoveLeftAsync()
    {
        if (CanMoveLeft && _onMoveLeft != null)
        {
            await _onMoveLeft(this);
        }
    }

    [RelayCommand]
    public async Task MoveRightAsync()
    {
        if (CanMoveRight && _onMoveRight != null)
        {
            await _onMoveRight(this);
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (_onDelete != null)
        {
            await _onDelete(this);
        }
    }
}

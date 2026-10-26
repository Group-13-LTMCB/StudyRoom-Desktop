using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;
using Wpf.Ui.Appearance;

namespace StudyRoom.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;

    [ObservableProperty]
    private int _selectedNavIndex = 0; // 0 = Overview, 1 = Pomodoro, 2 = Kanban

    [ObservableProperty]
    private PomodoroRoomViewModel _pomodoroRoomVm;

    [ObservableProperty]
    private KanbanBoardViewModel _kanbanVm;

    [ObservableProperty]
    private string _greeting = "Chào mừng trở lại, Sally!";

    [ObservableProperty]
    private string _userName = "Sally";

    [ObservableProperty]
    private string _userMajor = "Khoa học máy tính";

    [ObservableProperty]
    private string _totalStudyTime = "128 giờ";

    [ObservableProperty]
    private string _rankBadge = "Top 5% Sinh viên";

    [ObservableProperty]
    private string _focusTimeToday = "0.0 giờ";

    [ObservableProperty]
    private string _focusTimeTrend = "+12% so với hôm qua";

    [ObservableProperty]
    private string _completedTasks = "0/0";

    [ObservableProperty]
    private string _completedTasksTrend = "Tiến độ hôm nay";

    [ObservableProperty]
    private string _studyStreak = "0 ngày";

    [ObservableProperty]
    private string _studyStreakTrend = "Kỷ lục cá nhân";

    [ObservableProperty]
    private ObservableCollection<DailyActivityPoint> _weeklyActivityPoints = [];

    [ObservableProperty]
    private string _selectedTimeframe = "1W";

    [ObservableProperty]
    private ObservableCollection<StudyRoomSummary> _activeRooms = [];

    [ObservableProperty]
    private StudyRoomSummary? _selectedRoom;

    [ObservableProperty]
    private string _joinRoomCode = string.Empty;

    [ObservableProperty]
    private string _newRoomName = string.Empty;

    [ObservableProperty]
    private string _newRoomSubject = string.Empty;

    [ObservableProperty]
    private ControlMode _newRoomControlMode = ControlMode.HostOnly;

    [ObservableProperty]
    private bool _isHostOnlyMode = true;

    [ObservableProperty]
    private bool _isEqualMode;

    partial void OnIsHostOnlyModeChanged(bool value)
    {
        if (value)
        {
            IsEqualMode = false;
            NewRoomControlMode = ControlMode.HostOnly;
        }
    }

    partial void OnIsEqualModeChanged(bool value)
    {
        if (value)
        {
            IsHostOnlyMode = false;
            NewRoomControlMode = ControlMode.Equal;
        }
    }

    [ObservableProperty]
    private int _selectedHubTabIndex = 0;

    [ObservableProperty]
    private string? _validationErrorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private string _currentThemeName = "Dark";

    [ObservableProperty]
    private string _themeLabel = "Chế độ tối";

    [ObservableProperty]
    private string _themeIcon = "🌙";

    public DashboardViewModel(
        IDashboardService dashboardService,
        IPomodoroEngine? pomodoroEngine = null,
        KanbanBoardViewModel? kanbanVm = null,
        IKanbanService? kanbanService = null,
        IFractionalIndexingHelper? indexingHelper = null)
    {
        _dashboardService = dashboardService;
        _pomodoroRoomVm = new PomodoroRoomViewModel(pomodoroEngine);
        _pomodoroRoomVm.RequestBackToDashboard += () => SelectedNavIndex = 0;

        _kanbanVm = kanbanVm ?? new KanbanBoardViewModel(kanbanService, indexingHelper);
        _kanbanVm.RequestBackToDashboard += () => SelectedNavIndex = 0;
    }

    [RelayCommand]
    public void NavigateTo(object? indexParam)
    {
        if (indexParam is int intVal)
        {
            SelectedNavIndex = intVal;
        }
        else if (indexParam is string str && int.TryParse(str, out var parsed))
        {
            SelectedNavIndex = parsed;
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var metrics = await _dashboardService.GetMetricsAsync();
            FocusTimeToday = $"{metrics.FocusHoursToday:F1} giờ";
            FocusTimeTrend = metrics.FocusTimeTrend;
            CompletedTasks = $"{metrics.CompletedTasksToday}/{metrics.TotalTasksToday}";
            CompletedTasksTrend = metrics.TaskProgressText;
            StudyStreak = $"{metrics.StudyStreakDays} ngày";
            StudyStreakTrend = metrics.StreakBadge;
            TotalStudyTime = $"{metrics.TotalStudyHours:F1} giờ";
            RankBadge = metrics.StudentRank;

            var weeklyActivity = await _dashboardService.GetWeeklyActivityAsync();
            WeeklyActivityPoints.Clear();
            foreach (var point in weeklyActivity)
            {
                WeeklyActivityPoints.Add(point);
            }

            var recentRooms = await _dashboardService.GetRecentRoomsAsync();
            ActiveRooms.Clear();
            foreach (var room in recentRooms)
            {
                ActiveRooms.Add(room);
            }
        }
        catch (Exception ex)
        {
            ValidationErrorMessage = $"Lỗi tải dữ liệu: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        CurrentThemeName = IsDarkMode ? "Dark" : "Light";
        ThemeLabel = IsDarkMode ? "Chế độ tối" : "Chế độ sáng";
        ThemeIcon = IsDarkMode ? "🌙" : "☀️";

        if (System.Windows.Application.Current != null)
        {
            try
            {
                ApplicationThemeManager.Apply(IsDarkMode ? ApplicationTheme.Dark : ApplicationTheme.Light);
            }
            catch
            {
                // Headless test runner or unattached window fallback
            }
        }
    }

    [RelayCommand]
    public void ChangeTimeframe(string? timeframe)
    {
        if (!string.IsNullOrWhiteSpace(timeframe))
        {
            SelectedTimeframe = timeframe.Trim();
        }
    }

    [RelayCommand]
    public async Task JoinRoomAsync()
    {
        ValidationErrorMessage = null;
        SuccessMessage = null;
        HasError = false;

        if (string.IsNullOrWhiteSpace(JoinRoomCode))
        {
            ValidationErrorMessage = "Vui lòng nhập mã phòng học.";
            HasError = true;
            return;
        }

        string code = JoinRoomCode.Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            ValidationErrorMessage = "Mã phòng phải gồm chính xác 6 chữ số.";
            HasError = true;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _dashboardService.JoinRoomByCodeAsync(code);
            if (result.IsFailure)
            {
                ValidationErrorMessage = result.Error;
                HasError = true;
            }
            else if (result.Value is { } room)
            {
                SuccessMessage = $"Đã tham gia phòng '{room.RoomName}' ({room.RoomId}) thành công!";
                HasError = false;
                JoinRoomCode = string.Empty;

                var existing = ActiveRooms.FirstOrDefault(r => r.RoomId == room.RoomId);
                if (existing is null)
                {
                    ActiveRooms.Insert(0, room);
                }
                else
                {
                    int index = ActiveRooms.IndexOf(existing);
                    ActiveRooms[index] = room;
                }
                SelectedRoom = room;
                PomodoroRoomVm.SetRoom(room);
                SelectedNavIndex = 1;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CreateRoomAsync()
    {
        ValidationErrorMessage = null;
        SuccessMessage = null;
        HasError = false;

        if (string.IsNullOrWhiteSpace(NewRoomName))
        {
            ValidationErrorMessage = "Vui lòng nhập tên phòng học.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(NewRoomSubject))
        {
            ValidationErrorMessage = "Vui lòng nhập môn học hoặc chủ đề.";
            HasError = true;
            return;
        }

        var request = new CreateRoomRequest
        {
            RoomName = NewRoomName.Trim(),
            Subject = NewRoomSubject.Trim(),
            ControlMode = NewRoomControlMode,
            MaxMembers = 8,
            FocusMinutes = 25,
            BreakMinutes = 5
        };

        IsBusy = true;
        try
        {
            var result = await _dashboardService.CreateRoomAsync(request);
            if (result.IsFailure)
            {
                ValidationErrorMessage = result.Error;
                HasError = true;
            }
            else if (result.Value is { } newRoom)
            {
                SuccessMessage = $"Đã tạo phòng '{newRoom.RoomName}' thành công! Mã phòng: {newRoom.RoomId}";
                HasError = false;
                NewRoomName = string.Empty;
                NewRoomSubject = string.Empty;
                IsHostOnlyMode = true;
                IsEqualMode = false;

                ActiveRooms.Insert(0, newRoom);
                SelectedRoom = newRoom;
                PomodoroRoomVm.SetRoom(newRoom);
                SelectedNavIndex = 1;
                SelectedHubTabIndex = 0;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task QuickJoinRoomAsync(StudyRoomSummary? room)
    {
        var target = room ?? SelectedRoom ?? ActiveRooms.FirstOrDefault();
        if (target is null)
        {
            ValidationErrorMessage = "Vui lòng chọn phòng học hoặc nhập mã phòng cần tham gia.";
            HasError = true;
            return;
        }

        JoinRoomCode = target.RoomId;
        await JoinRoomAsync();
    }

    [RelayCommand]
    public void SelectJoinTab()
    {
        SelectedHubTabIndex = 0;
    }

    [RelayCommand]
    public void SelectCreateTab()
    {
        SelectedHubTabIndex = 1;
    }
}

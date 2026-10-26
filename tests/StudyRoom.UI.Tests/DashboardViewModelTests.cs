using Microsoft.Extensions.DependencyInjection;
using StudyRoom.Application.Services;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Models;
using StudyRoom.UI.ViewModels;
using Xunit;

namespace StudyRoom.UI.Tests;

public class DashboardViewModelTests
{
    private static DashboardViewModel CreateViewModel(IEnumerable<StudyRoomSummary>? initialRooms = null)
    {
        var service = new MockDashboardService(initialRooms);
        return new DashboardViewModel(service);
    }

    [Fact]
    public void DashboardViewModel_Construction_SetsDefaultProperties()
    {
        var vm = CreateViewModel();

        Assert.Equal("Chào mừng trở lại, Sally!", vm.Greeting);
        Assert.Equal("Sally", vm.UserName);
        Assert.Equal("Khoa học máy tính", vm.UserMajor);
        Assert.True(vm.IsDarkMode);
        Assert.Equal("Dark", vm.CurrentThemeName);
        Assert.Equal("1W", vm.SelectedTimeframe);
        Assert.Equal(0, vm.SelectedHubTabIndex);
        Assert.Empty(vm.JoinRoomCode);
        Assert.Empty(vm.NewRoomName);
        Assert.Empty(vm.NewRoomSubject);
        Assert.False(vm.HasError);
        Assert.Null(vm.ValidationErrorMessage);
        Assert.Null(vm.SuccessMessage);
    }

    [Fact]
    public async Task DashboardViewModel_LoadDataAsync_PopulatesMetricsAndActivity()
    {
        var vm = CreateViewModel();

        await vm.LoadDataAsync();

        Assert.Equal("4.5 giờ", vm.FocusTimeToday);
        Assert.Equal("+12% so với hôm qua", vm.FocusTimeTrend);
        Assert.Equal("8/10", vm.CompletedTasks);
        Assert.Equal("8/10 việc hôm nay", vm.CompletedTasksTrend);
        Assert.Equal("7 ngày", vm.StudyStreak);
        Assert.Equal("🔥 Chuỗi 7 ngày", vm.StudyStreakTrend);
        Assert.Equal("128.5 giờ", vm.TotalStudyTime);
        Assert.Equal("Top 5% Toàn trường", vm.RankBadge);
        Assert.Equal(7, vm.WeeklyActivityPoints.Count);
        Assert.Equal(4, vm.ActiveRooms.Count);
    }

    [Fact]
    public void DashboardViewModel_ToggleTheme_SwitchesThemeAndState()
    {
        var vm = CreateViewModel();
        Assert.True(vm.IsDarkMode);
        Assert.Equal("Dark", vm.CurrentThemeName);

        vm.ToggleThemeCommand.Execute(null);

        Assert.False(vm.IsDarkMode);
        Assert.Equal("Light", vm.CurrentThemeName);

        vm.ToggleThemeCommand.Execute(null);

        Assert.True(vm.IsDarkMode);
        Assert.Equal("Dark", vm.CurrentThemeName);
    }

    [Fact]
    public void DashboardViewModel_ToggleTheme_Headless_DoesNotThrow()
    {
        var vm = CreateViewModel();
        var exception = Record.Exception(() =>
        {
            vm.ToggleThemeCommand.Execute(null);
            vm.ToggleThemeCommand.Execute(null);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task JoinRoomCommand_EmptyCode_SetsValidationError()
    {
        var vm = CreateViewModel();
        vm.JoinRoomCode = "";

        await vm.JoinRoomAsync();

        Assert.True(vm.HasError);
        Assert.Contains("nhập mã", vm.ValidationErrorMessage);
        Assert.Null(vm.SuccessMessage);
    }

    [Fact]
    public async Task JoinRoomCommand_WhitespaceCode_SetsValidationError()
    {
        var vm = CreateViewModel();
        vm.JoinRoomCode = "   ";

        await vm.JoinRoomAsync();

        Assert.True(vm.HasError);
        Assert.Contains("nhập mã", vm.ValidationErrorMessage);
    }

    [Fact]
    public async Task JoinRoomCommand_ShortCode_SetsValidationError()
    {
        var vm = CreateViewModel();
        vm.JoinRoomCode = "12345";

        await vm.JoinRoomAsync();

        Assert.True(vm.HasError);
        Assert.Contains("6 chữ số", vm.ValidationErrorMessage);
    }

    [Fact]
    public async Task JoinRoomCommand_NonDigitCode_SetsValidationError()
    {
        var vm = CreateViewModel();
        vm.JoinRoomCode = "12A456";

        await vm.JoinRoomAsync();

        Assert.True(vm.HasError);
        Assert.Contains("6 chữ số", vm.ValidationErrorMessage);
    }

    [Fact]
    public async Task JoinRoomCommand_ValidCode_JoinsRoomSuccessfully()
    {
        var vm = CreateViewModel();
        await vm.LoadDataAsync();
        vm.JoinRoomCode = "102938";

        await vm.JoinRoomAsync();

        Assert.False(vm.HasError);
        Assert.Null(vm.ValidationErrorMessage);
        Assert.NotNull(vm.SuccessMessage);
        Assert.Contains("Ôn thi Giải tích III", vm.SuccessMessage);
        Assert.Empty(vm.JoinRoomCode);
        Assert.NotNull(vm.SelectedRoom);
        Assert.Equal("102938", vm.SelectedRoom.RoomId);
    }

    [Fact]
    public async Task JoinRoomCommand_NonExistentCode_ShowsServiceError()
    {
        var vm = CreateViewModel();
        await vm.LoadDataAsync();
        vm.JoinRoomCode = "999999";

        await vm.JoinRoomAsync();

        Assert.True(vm.HasError);
        Assert.NotNull(vm.ValidationErrorMessage);
        Assert.Contains("Không tìm thấy", vm.ValidationErrorMessage);
        Assert.Null(vm.SuccessMessage);
    }

    [Fact]
    public async Task CreateRoomCommand_EmptyRoomName_SetsValidationError()
    {
        var vm = CreateViewModel();
        vm.NewRoomName = "";
        vm.NewRoomSubject = "Giải tích";

        await vm.CreateRoomAsync();

        Assert.True(vm.HasError);
        Assert.Contains("tên phòng", vm.ValidationErrorMessage);
    }

    [Fact]
    public async Task CreateRoomCommand_EmptySubject_SetsValidationError()
    {
        var vm = CreateViewModel();
        vm.NewRoomName = "Phòng tự học đêm";
        vm.NewRoomSubject = "";

        await vm.CreateRoomAsync();

        Assert.True(vm.HasError);
        Assert.Contains("môn học", vm.ValidationErrorMessage);
    }

    [Fact]
    public async Task CreateRoomCommand_ValidInputs_CreatesRoomAndAddsToActiveRooms()
    {
        var vm = CreateViewModel();
        await vm.LoadDataAsync();
        int initialCount = vm.ActiveRooms.Count;

        vm.NewRoomName = "Nhóm Luyện Thuật Toán LeetCode";
        vm.NewRoomSubject = "Thuật toán";
        vm.NewRoomControlMode = ControlMode.Equal;

        await vm.CreateRoomAsync();

        Assert.False(vm.HasError);
        Assert.Null(vm.ValidationErrorMessage);
        Assert.NotNull(vm.SuccessMessage);
        Assert.Contains("Nhóm Luyện Thuật Toán LeetCode", vm.SuccessMessage);
        Assert.Empty(vm.NewRoomName);
        Assert.Empty(vm.NewRoomSubject);
        Assert.Equal(initialCount + 1, vm.ActiveRooms.Count);
        Assert.Equal("Nhóm Luyện Thuật Toán LeetCode", vm.ActiveRooms[0].RoomName);
        Assert.Equal(0, vm.SelectedHubTabIndex);
    }

    [Fact]
    public async Task CreateRoomCommand_WhenServiceFails_ShowsError()
    {
        var vm = CreateViewModel();
        vm.NewRoomName = "   ";
        vm.NewRoomSubject = "   ";

        await vm.CreateRoomAsync();

        Assert.True(vm.HasError);
        Assert.NotNull(vm.ValidationErrorMessage);
    }

    [Fact]
    public async Task QuickJoinRoomCommand_WithValidRoom_SetsCodeAndJoins()
    {
        var vm = CreateViewModel();
        await vm.LoadDataAsync();
        var room = vm.ActiveRooms[1];

        await vm.QuickJoinRoomAsync(room);

        Assert.False(vm.HasError);
        Assert.NotNull(vm.SuccessMessage);
        Assert.Contains(room.RoomName, vm.SuccessMessage);
        Assert.Equal(room.RoomId, vm.SelectedRoom?.RoomId);
    }

    [Fact]
    public async Task QuickJoinRoomCommand_WithNullRoomAndNoSelection_SetsError()
    {
        var vm = CreateViewModel();
        vm.SelectedRoom = null;

        await vm.QuickJoinRoomAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("chọn phòng", vm.ValidationErrorMessage);
    }

    [Fact]
    public void TimeframeSelection_UpdatesSelectedTimeframe()
    {
        var vm = CreateViewModel();
        Assert.Equal("1W", vm.SelectedTimeframe);

        vm.ChangeTimeframeCommand.Execute("1M");
        Assert.Equal("1M", vm.SelectedTimeframe);

        vm.ChangeTimeframeCommand.Execute("1Y");
        Assert.Equal("1Y", vm.SelectedTimeframe);

        vm.ChangeTimeframeCommand.Execute("ALL");
        Assert.Equal("ALL", vm.SelectedTimeframe);
    }

    [Fact]
    public async Task ValidationErrorMessage_ClearsWhenNewValidActionSucceeds()
    {
        var vm = CreateViewModel();
        vm.JoinRoomCode = "99";
        await vm.JoinRoomAsync();
        Assert.True(vm.HasError);
        Assert.NotNull(vm.ValidationErrorMessage);

        vm.NewRoomName = "Học Nhóm Sáng";
        vm.NewRoomSubject = "Vật lý";
        await vm.CreateRoomAsync();

        Assert.False(vm.HasError);
        Assert.Null(vm.ValidationErrorMessage);
        Assert.NotNull(vm.SuccessMessage);
    }

    [Fact]
    public void ToggleTheme_UpdatesThemeLabelAndIcon()
    {
        var vm = CreateViewModel();
        Assert.Equal("Chế độ tối", vm.ThemeLabel);
        Assert.Equal("🌙", vm.ThemeIcon);

        vm.ToggleThemeCommand.Execute(null);

        Assert.Equal("Chế độ sáng", vm.ThemeLabel);
        Assert.Equal("☀️", vm.ThemeIcon);

        vm.ToggleThemeCommand.Execute(null);

        Assert.Equal("Chế độ tối", vm.ThemeLabel);
        Assert.Equal("🌙", vm.ThemeIcon);
    }

    [Fact]
    public void ControlModeProperties_SynchronizeWithRadioFlags()
    {
        var vm = CreateViewModel();
        Assert.True(vm.IsHostOnlyMode);
        Assert.False(vm.IsEqualMode);
        Assert.Equal(ControlMode.HostOnly, vm.NewRoomControlMode);

        vm.IsEqualMode = true;

        Assert.False(vm.IsHostOnlyMode);
        Assert.True(vm.IsEqualMode);
        Assert.Equal(ControlMode.Equal, vm.NewRoomControlMode);

        vm.IsHostOnlyMode = true;

        Assert.True(vm.IsHostOnlyMode);
        Assert.False(vm.IsEqualMode);
        Assert.Equal(ControlMode.HostOnly, vm.NewRoomControlMode);
    }

    [Fact]
    public async Task CreateRoomCommand_WithEqualMode_PassesCorrectModeAndResets()
    {
        var vm = CreateViewModel();
        vm.NewRoomName = "Phòng Thảo Luận Mở";
        vm.NewRoomSubject = "Triết học";
        vm.IsEqualMode = true;

        await vm.CreateRoomAsync();

        Assert.False(vm.HasError);
        Assert.NotNull(vm.SuccessMessage);
        Assert.True(vm.IsHostOnlyMode);
        Assert.False(vm.IsEqualMode);
    }

    [Fact]
    public async Task QuickJoinRoomCommand_FallbackToFirstActiveRoom_WhenNoRoomPassed()
    {
        var vm = CreateViewModel();
        await vm.LoadDataAsync();
        vm.SelectedRoom = null;

        await vm.QuickJoinRoomAsync(null);

        Assert.False(vm.HasError);
        Assert.NotNull(vm.SuccessMessage);
        Assert.Equal("102938", vm.SelectedRoom?.RoomId);
        Assert.Equal(1, vm.SelectedNavIndex);
        Assert.Equal("102938", vm.PomodoroRoomVm.RoomId);
    }

    [Fact]
    public void DashboardViewModel_NavigateTo_ChangesSelectedNavIndex()
    {
        var vm = CreateViewModel();

        Assert.Equal(0, vm.SelectedNavIndex);

        vm.NavigateTo(1);
        Assert.Equal(1, vm.SelectedNavIndex);

        vm.NavigateTo(0);
        Assert.Equal(0, vm.SelectedNavIndex);
    }

    [Fact]
    public void DashboardViewModel_PomodoroRoomVm_RequestBack_RestoresOverview()
    {
        var vm = CreateViewModel();
        vm.NavigateTo(1);
        Assert.Equal(1, vm.SelectedNavIndex);

        vm.PomodoroRoomVm.BackToDashboard();
        Assert.Equal(0, vm.SelectedNavIndex);
    }

    [Fact]
    public async Task LoadDataAsync_WhenServiceThrows_SetsHasErrorWithoutCrashing()
    {
        var mockService = new ThrowingDashboardService();
        var vm = new DashboardViewModel(mockService);

        await vm.LoadDataAsync();

        Assert.True(vm.HasError);
        Assert.NotNull(vm.ValidationErrorMessage);
        Assert.Contains("Lỗi tải dữ liệu", vm.ValidationErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void MainWindow_CanBeResolvedFromDI_WithoutAmbiguity()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<StudyRoom.Application.Interfaces.IDashboardService, MockDashboardService>();
        services.AddTransient<StudyRoom.Application.Interfaces.IPomodoroTimerService, StudyRoom.Application.Services.LocalPomodoroTimerService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<MainWindow>();
        var provider = services.BuildServiceProvider();

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = provider.GetRequiredService<MainWindow>();
                Assert.NotNull(window);
                Assert.IsType<DashboardViewModel>(window.DataContext);
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

    private sealed class ThrowingDashboardService : StudyRoom.Application.Interfaces.IDashboardService
    {
        public Task<DashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated connection timeout");

        public Task<IReadOnlyList<DailyActivityPoint>> GetWeeklyActivityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyActivityPoint>>([]);

        public Task<IReadOnlyList<StudyRoomSummary>> GetRecentRoomsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudyRoomSummary>>([]);

        public Task<StudyRoom.Core.Common.Result<StudyRoomSummary>> JoinRoomByCodeAsync(string roomCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(StudyRoom.Core.Common.Result<StudyRoomSummary>.Failure("Error"));

        public Task<StudyRoom.Core.Common.Result<StudyRoomSummary>> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(StudyRoom.Core.Common.Result<StudyRoomSummary>.Failure("Error"));
    }
}

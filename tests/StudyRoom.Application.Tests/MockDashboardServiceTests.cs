using StudyRoom.Application.Services;
using StudyRoom.Core.Enums;
using StudyRoom.Core.Models;
using Xunit;

namespace StudyRoom.Application.Tests;

public class MockDashboardServiceTests
{
    [Fact]
    public async Task GetMetricsAsync_ReturnsRealisticMetrics()
    {
        var service = new MockDashboardService();
        var metrics = await service.GetMetricsAsync();

        Assert.Equal(4.5, metrics.FocusHoursToday);
        Assert.Equal(8, metrics.CompletedTasksToday);
        Assert.Equal(10, metrics.TotalTasksToday);
        Assert.Equal(7, metrics.StudyStreakDays);
        Assert.Equal(128.5, metrics.TotalStudyHours);
        Assert.Contains("Top 5%", metrics.StudentRank);
    }

    [Fact]
    public async Task GetWeeklyActivityAsync_ReturnsSevenDays()
    {
        var service = new MockDashboardService();
        var activity = await service.GetWeeklyActivityAsync();

        Assert.Equal(7, activity.Count);
        Assert.Equal("T2", activity[0].DayLabel);
        Assert.Equal("CN", activity[6].DayLabel);
        Assert.All(activity, p => Assert.True(p.Hours > 0));
    }

    [Fact]
    public async Task GetRecentRoomsAsync_ReturnsConfiguredRooms()
    {
        var service = new MockDashboardService();
        var rooms = await service.GetRecentRoomsAsync();

        Assert.Equal(4, rooms.Count);
        Assert.Contains(rooms, r => r.RoomId == "102938");
    }

    [Fact]
    public async Task JoinRoomByCodeAsync_WithValidCode_IncrementsMembersCount()
    {
        var service = new MockDashboardService();
        var roomsBefore = await service.GetRecentRoomsAsync();
        int initialMembers = roomsBefore.First(r => r.RoomId == "102938").MembersCount;

        var result = await service.JoinRoomByCodeAsync("102938");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(initialMembers + 1, result.Value.MembersCount);
    }

    [Fact]
    public async Task JoinRoomByCodeAsync_WhenRoomIsFull_ReturnsFailure()
    {
        var fullRoom = new StudyRoomSummary
        {
            RoomId = "555555",
            RoomName = "Phòng Đầy",
            Subject = "Test",
            MembersCount = 4,
            MaxMembers = 4,
            IsActive = true,
            HostName = "Test"
        };
        var service = new MockDashboardService([fullRoom]);

        var result = await service.JoinRoomByCodeAsync("555555");

        Assert.True(result.IsFailure);
        Assert.Contains("đã đầy", result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("1234567")]
    [InlineData("ABC123")]
    public async Task JoinRoomByCodeAsync_WithInvalidCodeFormat_ReturnsFailure(string code)
    {
        var service = new MockDashboardService();
        var result = await service.JoinRoomByCodeAsync(code);

        Assert.True(result.IsFailure);
        Assert.Contains("6 chữ số", result.Error);
    }

    [Fact]
    public async Task JoinRoomByCodeAsync_WithNonExistentCode_ReturnsFailure()
    {
        var service = new MockDashboardService();
        var result = await service.JoinRoomByCodeAsync("999999");

        Assert.True(result.IsFailure);
        Assert.Contains("Không tìm thấy", result.Error);
    }

    [Fact]
    public async Task CreateRoomAsync_WithValidRequest_CreatesRoomAndPrepends()
    {
        var service = new MockDashboardService();
        var request = new CreateRoomRequest
        {
            RoomName = "Lập trình C# Nâng cao",
            Subject = "Công nghệ phần mềm",
            ControlMode = ControlMode.Equal,
            MaxMembers = 6,
            FocusMinutes = 45,
            BreakMinutes = 10
        };

        var result = await service.CreateRoomAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Lập trình C# Nâng cao", result.Value.RoomName);
        Assert.Equal(6, result.Value.RoomId.Length);
        Assert.All(result.Value.RoomId, c => Assert.True(char.IsDigit(c)));

        var recentRooms = await service.GetRecentRoomsAsync();
        Assert.Equal(result.Value.RoomId, recentRooms[0].RoomId);
    }

    [Theory]
    [InlineData("", "Toán")]
    [InlineData("   ", "Toán")]
    public async Task CreateRoomAsync_WithEmptyName_ReturnsFailure(string roomName, string subject)
    {
        var service = new MockDashboardService();
        var request = new CreateRoomRequest { RoomName = roomName, Subject = subject };

        var result = await service.CreateRoomAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("Tên phòng học không được để trống", result.Error);
    }

    [Theory]
    [InlineData("Phòng 1", "")]
    [InlineData("Phòng 1", "   ")]
    public async Task CreateRoomAsync_WithEmptySubject_ReturnsFailure(string roomName, string subject)
    {
        var service = new MockDashboardService();
        var request = new CreateRoomRequest { RoomName = roomName, Subject = subject };

        var result = await service.CreateRoomAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("Môn học không được để trống", result.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(51)]
    [InlineData(0)]
    public async Task CreateRoomAsync_WithInvalidMaxMembers_ReturnsFailure(int maxMembers)
    {
        var service = new MockDashboardService();
        var request = new CreateRoomRequest
        {
            RoomName = "Phòng hợp lệ",
            Subject = "Môn hợp lệ",
            MaxMembers = maxMembers
        };

        var result = await service.CreateRoomAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("2 đến 50", result.Error);
    }

    [Fact]
    public async Task CreateRoomAsync_WithNullRequest_ReturnsFailure()
    {
        var service = new MockDashboardService();
        var result = await service.CreateRoomAsync(null!);

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }
}

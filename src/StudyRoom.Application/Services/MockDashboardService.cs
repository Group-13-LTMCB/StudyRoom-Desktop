using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Common;
using StudyRoom.Core.Models;

namespace StudyRoom.Application.Services;

public sealed class MockDashboardService : IDashboardService
{
    private readonly List<StudyRoomSummary> _rooms = [];
    private readonly object _lock = new();

    public MockDashboardService(IEnumerable<StudyRoomSummary>? initialRooms = null)
    {
        if (initialRooms != null)
        {
            _rooms.AddRange(initialRooms);
        }
        else
        {
            _rooms.AddRange([
                new StudyRoomSummary
                {
                    RoomId = "102938",
                    RoomName = "Ôn thi Giải tích III",
                    Subject = "Toán cao cấp",
                    MembersCount = 4,
                    MaxMembers = 8,
                    IsActive = true,
                    HostName = "Sally"
                },
                new StudyRoomSummary
                {
                    RoomId = "204859",
                    RoomName = "Cấu trúc dữ liệu & GT",
                    Subject = "Khoa học máy tính",
                    MembersCount = 6,
                    MaxMembers = 10,
                    IsActive = true,
                    HostName = "Alex"
                },
                new StudyRoomSummary
                {
                    RoomId = "319204",
                    RoomName = "IELTS Speaking 7.5+",
                    Subject = "Ngoại ngữ",
                    MembersCount = 3,
                    MaxMembers = 5,
                    IsActive = true,
                    HostName = "Minh"
                },
                new StudyRoomSummary
                {
                    RoomId = "445892",
                    RoomName = "Machine Learning Lab",
                    Subject = "AI / Data",
                    MembersCount = 5,
                    MaxMembers = 6,
                    IsActive = true,
                    HostName = "Elena"
                }
            ]);
        }
    }

    public Task<DashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new DashboardMetrics
        {
            FocusHoursToday = 4.5,
            FocusTimeTrend = "+12% so với hôm qua",
            CompletedTasksToday = 8,
            TotalTasksToday = 10,
            TaskProgressText = "8/10 việc hôm nay",
            StudyStreakDays = 7,
            StreakBadge = "🔥 Chuỗi 7 ngày",
            TotalStudyHours = 128.5,
            StudentRank = "Top 5% Toàn trường"
        };

        return Task.FromResult(metrics);
    }

    public Task<IReadOnlyList<DailyActivityPoint>> GetWeeklyActivityAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        // Monday through Sunday
        int currentDayOfWeek = (int)today.DayOfWeek;
        int diffToMonday = currentDayOfWeek == 0 ? -6 : 1 - currentDayOfWeek;
        var monday = today.AddDays(diffToMonday);

        IReadOnlyList<DailyActivityPoint> points = [
            new DailyActivityPoint { DayLabel = "T2", Hours = 3.5, Date = monday },
            new DailyActivityPoint { DayLabel = "T3", Hours = 5.0, Date = monday.AddDays(1) },
            new DailyActivityPoint { DayLabel = "T4", Hours = 4.2, Date = monday.AddDays(2) },
            new DailyActivityPoint { DayLabel = "T5", Hours = 6.8, Date = monday.AddDays(3) },
            new DailyActivityPoint { DayLabel = "T6", Hours = 4.0, Date = monday.AddDays(4) },
            new DailyActivityPoint { DayLabel = "T7", Hours = 7.5, Date = monday.AddDays(5) },
            new DailyActivityPoint { DayLabel = "CN", Hours = 4.5, Date = monday.AddDays(6) },
        ];

        return Task.FromResult(points);
    }

    public Task<IReadOnlyList<StudyRoomSummary>> GetRecentRoomsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<StudyRoomSummary> copy = _rooms.ToList().AsReadOnly();
            return Task.FromResult(copy);
        }
    }

    public Task<Result<StudyRoomSummary>> JoinRoomByCodeAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Trim().Length != 6 || !roomCode.Trim().All(char.IsDigit))
        {
            return Task.FromResult(Result<StudyRoomSummary>.Failure("Mã phòng phải gồm 6 chữ số hợp lệ."));
        }

        string trimmedCode = roomCode.Trim();

        lock (_lock)
        {
            var room = _rooms.FirstOrDefault(r => r.RoomId == trimmedCode);
            if (room is null)
            {
                return Task.FromResult(Result<StudyRoomSummary>.Failure($"Không tìm thấy phòng học với mã {trimmedCode}."));
            }

            if (room.MembersCount >= room.MaxMembers)
            {
                return Task.FromResult(Result<StudyRoomSummary>.Failure("Phòng học đã đầy."));
            }

            room.MembersCount++;
            return Task.FromResult(Result<StudyRoomSummary>.Success(room));
        }
    }

    public Task<Result<StudyRoomSummary>> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(Result<StudyRoomSummary>.Failure("Yêu cầu tạo phòng không hợp lệ."));
        }

        if (string.IsNullOrWhiteSpace(request.RoomName))
        {
            return Task.FromResult(Result<StudyRoomSummary>.Failure("Tên phòng học không được để trống."));
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return Task.FromResult(Result<StudyRoomSummary>.Failure("Môn học không được để trống."));
        }

        if (request.MaxMembers < 2 || request.MaxMembers > 50)
        {
            return Task.FromResult(Result<StudyRoomSummary>.Failure("Số thành viên tối đa phải từ 2 đến 50."));
        }

        lock (_lock)
        {
            string newId;
            do
            {
                newId = Random.Shared.Next(100000, 999999).ToString();
            }
            while (_rooms.Any(r => r.RoomId == newId));

            var newRoom = new StudyRoomSummary
            {
                RoomId = newId,
                RoomName = request.RoomName.Trim(),
                Subject = request.Subject.Trim(),
                MembersCount = 1,
                MaxMembers = request.MaxMembers,
                IsActive = true,
                HostName = "Bạn (Sally)"
            };

            _rooms.Insert(0, newRoom);
            return Task.FromResult(Result<StudyRoomSummary>.Success(newRoom));
        }
    }
}

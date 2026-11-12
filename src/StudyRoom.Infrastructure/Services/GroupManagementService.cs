using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Common;
using StudyRoom.Core.Models;

namespace StudyRoom.Infrastructure.Services;

public sealed class GroupManagementService : IGroupManagementService
{
    private readonly ILocalCacheRepository _cacheRepository;
    private readonly IDashboardService? _dashboardService;
    private static readonly string[] Palette = ["#6366F1", "#10B981", "#F59E0B", "#EC4899", "#8B5CF6", "#06B6D4", "#3B82F6"];

    public GroupManagementService(ILocalCacheRepository cacheRepository, IDashboardService? dashboardService = null)
    {
        _cacheRepository = cacheRepository;
        _dashboardService = dashboardService;
    }

    public async Task<IReadOnlyList<GroupNavigationItem>> GetUserGroupsAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cacheRepository.GetCachedUserGroupsAsync(cancellationToken);
        if (cached.Count > 0)
        {
            return cached;
        }

        // Seed with initial sample study groups
        var defaultGroups = new List<GroupNavigationItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Ôn thi Giải tích III",
                Subject = "Toán cao cấp",
                AccentColor = "#6366F1",
                JoinCode = "102938",
                MemberCount = 4,
                IsHost = false,
                HostName = "Sally",
                IsPinned = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Cấu trúc dữ liệu & GT",
                Subject = "Khoa học máy tính",
                AccentColor = "#10B981",
                JoinCode = "204859",
                MemberCount = 6,
                IsHost = true,
                HostName = "Bạn",
                IsPinned = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "IELTS Speaking 7.5+",
                Subject = "Ngoại ngữ",
                AccentColor = "#F59E0B",
                JoinCode = "319204",
                MemberCount = 3,
                IsHost = false,
                HostName = "Minh",
                IsPinned = false
            }
        };

        await _cacheRepository.CacheUserGroupsAsync(defaultGroups, cancellationToken);
        return defaultGroups;
    }

    public async Task<Result<GroupNavigationItem>> JoinGroupByCodeAsync(string joinCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return Result<GroupNavigationItem>.Failure("Vui lòng nhập mã phòng học.");
        }

        string cleanCode = joinCode.Trim();
        if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
        {
            return Result<GroupNavigationItem>.Failure("Mã phòng học phải gồm chính xác 6 chữ số.");
        }

        var existing = await _cacheRepository.GetCachedUserGroupsAsync(cancellationToken);
        var found = existing.FirstOrDefault(g => g.JoinCode == cleanCode);
        if (found != null)
        {
            return Result<GroupNavigationItem>.Success(found);
        }

        // Check explore catalog or dashboard service rooms
        var explore = await GetExploreGroupsAsync(cancellationToken);
        var matched = explore.FirstOrDefault(g => g.JoinCode == cleanCode);

        var newGroup = matched ?? new GroupNavigationItem
        {
            Id = Guid.NewGuid(),
            Name = $"Phòng học #{cleanCode}",
            Subject = "Học tập tổng hợp",
            AccentColor = Palette[Math.Abs(cleanCode.GetHashCode()) % Palette.Length],
            JoinCode = cleanCode,
            MemberCount = 2,
            IsHost = false,
            HostName = "Chủ phòng"
        };

        var toCache = new List<GroupNavigationItem>(existing) { newGroup };
        await _cacheRepository.CacheUserGroupsAsync(toCache, cancellationToken);

        return Result<GroupNavigationItem>.Success(newGroup);
    }

    public async Task<Result<GroupNavigationItem>> CreateGroupAsync(string name, string subject, int maxMembers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<GroupNavigationItem>.Failure("Vui lòng nhập tên nhóm học tập.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result<GroupNavigationItem>.Failure("Vui lòng nhập môn học hoặc chủ đề.");
        }

        string joinCode = Random.Shared.Next(100000, 999999).ToString();
        var newGroup = new GroupNavigationItem
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Subject = subject.Trim(),
            AccentColor = Palette[Math.Abs(name.GetHashCode()) % Palette.Length],
            JoinCode = joinCode,
            MemberCount = 1,
            IsHost = true,
            HostName = "Bạn",
            IsPinned = false
        };

        var existing = await _cacheRepository.GetCachedUserGroupsAsync(cancellationToken);
        var toCache = new List<GroupNavigationItem>(existing) { newGroup };
        await _cacheRepository.CacheUserGroupsAsync(toCache, cancellationToken);

        return Result<GroupNavigationItem>.Success(newGroup);
    }

    public async Task<Result> LeaveGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await _cacheRepository.RemoveCachedUserGroupAsync(groupId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DisbandGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await _cacheRepository.RemoveCachedUserGroupAsync(groupId, cancellationToken);
        return Result.Success();
    }

    public Task<IReadOnlyList<GroupNavigationItem>> GetExploreGroupsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GroupNavigationItem> exploreList = [
            new GroupNavigationItem
            {
                Id = Guid.NewGuid(),
                Name = "Machine Learning & AI Lab",
                Subject = "Trí tuệ nhân tạo",
                AccentColor = "#8B5CF6",
                JoinCode = "445892",
                MemberCount = 5,
                HostName = "Elena",
                IsHost = false
            },
            new GroupNavigationItem
            {
                Id = Guid.NewGuid(),
                Name = "Lập trình C# & Cloud AWS",
                Subject = "Kỹ thuật phần mềm",
                AccentColor = "#06B6D4",
                JoinCode = "556712",
                MemberCount = 7,
                HostName = "Huy Hoàng",
                IsHost = false
            },
            new GroupNavigationItem
            {
                Id = Guid.NewGuid(),
                Name = "Luyện đề THPT Quốc Gia",
                Subject = "Toán - Lý - Hóa",
                AccentColor = "#EC4899",
                JoinCode = "667823",
                MemberCount = 8,
                HostName = "Thầy Nam",
                IsHost = false
            },
            new GroupNavigationItem
            {
                Id = Guid.NewGuid(),
                Name = "Deep Work & Focus Room",
                Subject = "Năng suất cá nhân",
                AccentColor = "#10B981",
                JoinCode = "778934",
                MemberCount = 4,
                HostName = "Chloe",
                IsHost = false
            }
        ];

        return Task.FromResult(exploreList);
    }

    public async Task PinGroupAsync(Guid groupId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var existing = await _cacheRepository.GetCachedUserGroupsAsync(cancellationToken);
        var target = existing.FirstOrDefault(g => g.Id == groupId);
        if (target != null)
        {
            target.IsPinned = isPinned;
            await _cacheRepository.CacheUserGroupsAsync(existing, cancellationToken);
        }
    }

    public async Task MuteGroupAsync(Guid groupId, bool isMuted, CancellationToken cancellationToken = default)
    {
        var existing = await _cacheRepository.GetCachedUserGroupsAsync(cancellationToken);
        var target = existing.FirstOrDefault(g => g.Id == groupId);
        if (target != null)
        {
            target.IsMuted = isMuted;
            await _cacheRepository.CacheUserGroupsAsync(existing, cancellationToken);
        }
    }
}

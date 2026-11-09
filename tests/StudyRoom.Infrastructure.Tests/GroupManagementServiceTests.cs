using StudyRoom.Application.Interfaces;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Models;
using StudyRoom.Infrastructure.Services;
using Xunit;

namespace StudyRoom.Infrastructure.Tests;

public class GroupManagementServiceTests
{
    private class FakeCacheRepo : ILocalCacheRepository
    {
        private readonly List<GroupNavigationItem> _groups = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CacheCardsAsync(string groupId, IEnumerable<KanbanCard> cards, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<KanbanCard>> GetCachedCardsAsync(string groupId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KanbanCard>>([]);

        public Task CacheUserGroupsAsync(IEnumerable<GroupNavigationItem> groups, CancellationToken cancellationToken = default)
        {
            _groups.Clear();
            _groups.AddRange(groups);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GroupNavigationItem>> GetCachedUserGroupsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<GroupNavigationItem>>(_groups.ToList());
        }

        public Task RemoveCachedUserGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            _groups.RemoveAll(g => g.Id == groupId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetUserGroupsAsync_WhenEmpty_SeedsAndReturnsDefaultGroups()
    {
        var repo = new FakeCacheRepo();
        var service = new GroupManagementService(repo);

        var groups = await service.GetUserGroupsAsync();

        Assert.NotEmpty(groups);
        Assert.Contains(groups, g => g.Name.Contains("Giải tích"));
        Assert.Contains(groups, g => g.Name.Contains("Cấu trúc dữ liệu"));
    }

    [Fact]
    public async Task JoinGroupByCodeAsync_InvalidCode_ReturnsFailure()
    {
        var repo = new FakeCacheRepo();
        var service = new GroupManagementService(repo);

        var resultEmpty = await service.JoinGroupByCodeAsync("");
        var resultShort = await service.JoinGroupByCodeAsync("123");
        var resultNonDigits = await service.JoinGroupByCodeAsync("ABC123");

        Assert.True(resultEmpty.IsFailure);
        Assert.True(resultShort.IsFailure);
        Assert.True(resultNonDigits.IsFailure);
    }

    [Fact]
    public async Task JoinGroupByCodeAsync_ValidCode_ReturnsSuccessAndCaches()
    {
        var repo = new FakeCacheRepo();
        var service = new GroupManagementService(repo);

        var result = await service.JoinGroupByCodeAsync("888999");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("888999", result.Value!.JoinCode);

        var cached = await repo.GetCachedUserGroupsAsync();
        Assert.Contains(cached, g => g.JoinCode == "888999");
    }

    [Fact]
    public async Task CreateGroupAsync_EmptyInput_ReturnsFailure()
    {
        var repo = new FakeCacheRepo();
        var service = new GroupManagementService(repo);

        var res1 = await service.CreateGroupAsync("", "Subject", 5);
        var res2 = await service.CreateGroupAsync("Name", "", 5);

        Assert.True(res1.IsFailure);
        Assert.True(res2.IsFailure);
    }

    [Fact]
    public async Task CreateGroupAsync_ValidInput_CreatesHostGroup()
    {
        var repo = new FakeCacheRepo();
        var service = new GroupManagementService(repo);

        var result = await service.CreateGroupAsync("Học Vue & React", "Frontend", 10);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Học Vue & React", result.Value!.Name);
        Assert.Equal("Frontend", result.Value.Subject);
        Assert.True(result.Value.IsHost);
        Assert.Equal(6, result.Value.JoinCode.Length);
    }

    [Fact]
    public async Task LeaveGroupAsync_RemovesFromCache()
    {
        var repo = new FakeCacheRepo();
        var service = new GroupManagementService(repo);

        var group = (await service.CreateGroupAsync("Test Group", "Test", 5)).Value!;
        await service.LeaveGroupAsync(group.Id);

        var remaining = await repo.GetCachedUserGroupsAsync();
        Assert.DoesNotContain(remaining, g => g.Id == group.Id);
    }

    [Fact]
    public async Task PinGroupAsync_TogglesIsPinned()
    {
        var repo = new FakeCacheRepo();
        var service = new GroupManagementService(repo);

        var group = (await service.CreateGroupAsync("Pinned Group", "Test", 5)).Value!;
        Assert.False(group.IsPinned);

        await service.PinGroupAsync(group.Id, true);

        var updated = await repo.GetCachedUserGroupsAsync();
        Assert.True(updated.First(g => g.Id == group.Id).IsPinned);
    }
}

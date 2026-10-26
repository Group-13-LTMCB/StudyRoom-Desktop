using Microsoft.Data.Sqlite;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using StudyRoom.Infrastructure.Storage;

namespace StudyRoom.Infrastructure.Tests;

public class SqliteLocalCacheRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteLocalCacheRepository _repository;

    public SqliteLocalCacheRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"studyroom_cache_test_{Guid.NewGuid():N}.db");
        _repository = new SqliteLocalCacheRepository(_testDbPath);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateTablesSuccessfully()
    {
        await _repository.InitializeAsync();
        Assert.True(File.Exists(_testDbPath));
    }

    [Fact]
    public async Task SaveSettingAsync_And_GetSettingAsync_ShouldPersistAndRetrieveValue()
    {
        await _repository.InitializeAsync();

        await _repository.SaveSettingAsync("theme", "dark");
        string? theme = await _repository.GetSettingAsync("theme");
        Assert.Equal("dark", theme);

        // Update existing key
        await _repository.SaveSettingAsync("theme", "light");
        string? updatedTheme = await _repository.GetSettingAsync("theme");
        Assert.Equal("light", updatedTheme);

        // Non-existent key
        string? missing = await _repository.GetSettingAsync("non_existent");
        Assert.Null(missing);
    }

    [Fact]
    public async Task CacheCardsAsync_And_GetCachedCardsAsync_ShouldPreserveOrderAndData()
    {
        await _repository.InitializeAsync();

        string groupId = Guid.NewGuid().ToString("N");
        var card1 = new KanbanCard
        {
            GroupId = Guid.Parse(groupId),
            Title = "Task 1",
            Column = KanbanColumnType.Todo,
            Rank = 2.0
        };
        var card2 = new KanbanCard
        {
            GroupId = Guid.Parse(groupId),
            Title = "Task 0",
            Column = KanbanColumnType.Todo,
            Rank = 1.0
        };
        var card3 = new KanbanCard
        {
            GroupId = Guid.Parse(groupId),
            Title = "In Progress Task",
            Column = KanbanColumnType.InProgress,
            Rank = 1.5
        };

        await _repository.CacheCardsAsync(groupId, new[] { card1, card2, card3 });

        var loaded = await _repository.GetCachedCardsAsync(groupId);

        Assert.Equal(3, loaded.Count);
        // Todo column, ordered by Rank ascending: Task 0 (1.0), then Task 1 (2.0)
        Assert.Equal("Task 0", loaded[0].Title);
        Assert.Equal(1.0, loaded[0].Rank);
        Assert.Equal(KanbanColumnType.Todo, loaded[0].Column);

        Assert.Equal("Task 1", loaded[1].Title);
        Assert.Equal(2.0, loaded[1].Rank);

        Assert.Equal("In Progress Task", loaded[2].Title);
        Assert.Equal(KanbanColumnType.InProgress, loaded[2].Column);
    }

    [Fact]
    public async Task CacheFlashcardsAsync_And_GetCachedFlashcardsAsync_ShouldWorkProperly()
    {
        await _repository.InitializeAsync();

        string groupId = Guid.NewGuid().ToString("N");
        var flashcard = new Flashcard
        {
            GroupId = Guid.Parse(groupId),
            Question = "What is DPAPI?",
            Answer = "Data Protection API in Windows.",
            LastReviewedAt = DateTimeOffset.UtcNow
        };

        await _repository.CacheFlashcardsAsync(groupId, new[] { flashcard });

        var loaded = await _repository.GetCachedFlashcardsAsync(groupId);

        Assert.Single(loaded);
        Assert.Equal(flashcard.Question, loaded[0].Question);
        Assert.Equal(flashcard.Answer, loaded[0].Answer);
        Assert.NotNull(loaded[0].LastReviewedAt);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

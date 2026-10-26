using StudyRoom.Application.Helpers;
using StudyRoom.Application.Interfaces;
using StudyRoom.Application.Services;
using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;
using Xunit;

namespace StudyRoom.Application.Tests;

public class KanbanServiceTests
{
    private class InMemoryCacheRepository : ILocalCacheRepository
    {
        public bool Initialized { get; private set; }
        public Dictionary<string, List<KanbanCard>> StoredCards { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task CacheCardsAsync(string groupId, IEnumerable<KanbanCard> cards, CancellationToken cancellationToken = default)
        {
            StoredCards[groupId] = cards.Select(c => new KanbanCard
            {
                Id = c.Id,
                GroupId = c.GroupId,
                Title = c.Title,
                Description = c.Description,
                Column = c.Column,
                Rank = c.Rank,
                OrderIndex = c.OrderIndex,
                Version = c.Version,
                UpdatedAt = c.UpdatedAt
            }).ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KanbanCard>> GetCachedCardsAsync(string groupId, CancellationToken cancellationToken = default)
        {
            if (StoredCards.TryGetValue(groupId, out var list))
            {
                return Task.FromResult<IReadOnlyList<KanbanCard>>(list.AsReadOnly());
            }

            return Task.FromResult<IReadOnlyList<KanbanCard>>(new List<KanbanCard>().AsReadOnly());
        }
    }

    private readonly FractionalIndexingHelper _indexingHelper = new();

    [Fact]
    public async Task GetCardsAsync_WhenCacheEmpty_InitializesDefaultCards()
    {
        var repo = new InMemoryCacheRepository();
        var service = new KanbanService(_indexingHelper, repo);

        var cards = await service.GetCardsAsync("group-test");

        Assert.True(repo.Initialized);
        Assert.NotEmpty(cards);
        Assert.Contains(cards, c => c.Column == KanbanColumnType.Todo);
        Assert.Contains(cards, c => c.Column == KanbanColumnType.InProgress);
        Assert.Contains(cards, c => c.Column == KanbanColumnType.Done);
        Assert.True(repo.StoredCards.ContainsKey("group-test"));
    }

    [Fact]
    public async Task AddCardAsync_AddsCardToEndOfColumnWithCorrectRank()
    {
        var service = new KanbanService(_indexingHelper);
        string groupId = "group-add";

        var initialCards = await service.GetCardsAsync(groupId);
        int initialTodoCount = initialCards.Count(c => c.Column == KanbanColumnType.Todo);
        double maxTodoRank = initialCards.Where(c => c.Column == KanbanColumnType.Todo).Max(c => c.Rank);

        var newCard = await service.AddCardAsync(groupId, "Nhiệm vụ mới", "Mô tả bài tập", KanbanColumnType.Todo);

        Assert.Equal("Nhiệm vụ mới", newCard.Title);
        Assert.Equal("Mô tả bài tập", newCard.Description);
        Assert.Equal(KanbanColumnType.Todo, newCard.Column);
        Assert.True(newCard.Rank > maxTodoRank);

        var allCards = await service.GetCardsAsync(groupId);
        Assert.Equal(initialTodoCount + 1, allCards.Count(c => c.Column == KanbanColumnType.Todo));
    }

    [Fact]
    public async Task MoveCardAsync_MovesCardToTargetColumnAndUpdatesRank()
    {
        var service = new KanbanService(_indexingHelper);
        string groupId = "group-move";

        var cards = await service.GetCardsAsync(groupId);
        var todoCard = cards.First(c => c.Column == KanbanColumnType.Todo);

        var moved = await service.MoveCardAsync(todoCard.Id, KanbanColumnType.InProgress, prevRank: 1.0, nextRank: 2.0);

        Assert.Equal(todoCard.Id, moved.Id);
        Assert.Equal(KanbanColumnType.InProgress, moved.Column);
        Assert.Equal(1.5, moved.Rank);
    }

    [Fact]
    public async Task MoveCardAsync_WhenNeedsRebalance_RebalancesRanks()
    {
        var service = new KanbanService(_indexingHelper);
        string groupId = "group-rebalance";

        var cards = await service.GetCardsAsync(groupId);
        var todoCard = cards.First(c => c.Column == KanbanColumnType.Todo);

        // Move with ranks so close that needs rebalance (< 1e-6)
        var moved = await service.MoveCardAsync(todoCard.Id, KanbanColumnType.Done, prevRank: 1.0, nextRank: 1.00000001);

        var updatedCards = await service.GetCardsAsync(groupId);
        var doneCards = updatedCards.Where(c => c.Column == KanbanColumnType.Done).OrderBy(c => c.Rank).ToList();

        // Ranks should be rebalanced (1.0, 2.0, ...)
        Assert.Equal(1.0, doneCards[0].Rank);
        Assert.Equal(2.0, doneCards[1].Rank);
    }

    [Fact]
    public async Task MoveCardAsync_WhenCardNotFound_ThrowsKeyNotFoundException()
    {
        var service = new KanbanService(_indexingHelper);
        await service.GetCardsAsync("group-1");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.MoveCardAsync(Guid.NewGuid(), KanbanColumnType.Done, null, null));
    }

    [Fact]
    public async Task DeleteCardAsync_RemovesCardFromService()
    {
        var repo = new InMemoryCacheRepository();
        var service = new KanbanService(_indexingHelper, repo);
        string groupId = "group-delete";

        var cards = await service.GetCardsAsync(groupId);
        var toDelete = cards.First();

        await service.DeleteCardAsync(toDelete.Id);

        var remaining = await service.GetCardsAsync(groupId);
        Assert.DoesNotContain(remaining, c => c.Id == toDelete.Id);
        Assert.DoesNotContain(repo.StoredCards[groupId], c => c.Id == toDelete.Id);
    }
}
